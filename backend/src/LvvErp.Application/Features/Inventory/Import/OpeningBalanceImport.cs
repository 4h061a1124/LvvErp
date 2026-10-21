using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Application.Common.Text;
using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Import;

public static class OpeningBalanceColumns
{
    public const string Material = "Material";
    public const string Warehouse = "Warehouse";
    public const string Location = "Location";
    public const string LotNumber = "LotNumber";
    public const string ManufactureDate = "ManufactureDate";
    public const string ExpiryDate = "ExpiryDate";
    public const string Quantity = "Quantity";

    public static readonly IReadOnlyList<ExcelColumn> All =
    [
        new(Material, "Mã vật tư *", 16, IsText: true, ListName: Material),
        new(Warehouse, "Kho *", 22, ListName: Warehouse),
        new(Location, "Vị trí", 12, IsText: true),
        new(LotNumber, "Số lô", 16, IsText: true),
        new(ManufactureDate, "Ngày sản xuất", 14),
        new(ExpiryDate, "Hạn dùng", 14),
        new(Quantity, "Số lượng *", 12),
    ];
}

public sealed record OpeningBalanceRow(
    int RowNumber, string? Material, string? Warehouse, string? Location, string? LotNumber,
    string? ManufactureDate, string? ExpiryDate, string? Quantity)
{
    public static OpeningBalanceRow From(ExcelRow r) => new(
        r.RowNumber, r[OpeningBalanceColumns.Material], r[OpeningBalanceColumns.Warehouse], r[OpeningBalanceColumns.Location],
        r[OpeningBalanceColumns.LotNumber], r[OpeningBalanceColumns.ManufactureDate], r[OpeningBalanceColumns.ExpiryDate],
        r[OpeningBalanceColumns.Quantity]);
}

public sealed record OpeningBalanceLine(
    int RowNumber, Guid MaterialId, Guid WarehouseId, Guid? LocationId, string? LotNumber,
    DateOnly? ManufactureDate, DateOnly? ExpiryDate, decimal Quantity);

public sealed record MaterialRef(Guid Id, bool TrackLots);
public sealed record LotDates(DateOnly? ManufactureDate, DateOnly? ExpiryDate);

/// <summary>Pure row validation for the opening balance import (no database access).</summary>
/// <param name="materialsByCode">Active materials by upper-case code.</param>
/// <param name="locations">Active locations by (warehouse, upper-case code).</param>
/// <param name="existingLots">Lots already in the system, to keep their dates consistent.</param>
public sealed class OpeningBalanceValidator(
    IReadOnlyDictionary<string, MaterialRef> materialsByCode,
    IEnumerable<LookupDto> warehouses,
    IReadOnlyDictionary<(Guid WarehouseId, string Code), Guid> locations,
    IReadOnlyDictionary<(Guid MaterialId, string LotNumber), LotDates> existingLots)
{
    private readonly LookupMatcher _warehouses = new(warehouses);

    public (IReadOnlyList<OpeningBalanceLine> Lines, IReadOnlyList<ImportError> Errors) Validate(IReadOnlyList<OpeningBalanceRow> rows)
    {
        var lines = new List<OpeningBalanceLine>();
        var errors = new List<ImportError>();
        var firstRowByKey = new Dictionary<(Guid, Guid, Guid?, string?), int>();
        var lotDatesInFile = new Dictionary<(Guid, string), (int Row, LotDates Dates)>();

        foreach (var row in rows)
        {
            var rowErrors = new List<ImportError>();
            void Error(string column, string code, string message, string? value = null) =>
                rowErrors.Add(new ImportError(row.RowNumber, column, code, message, value));

            MaterialRef? material = null;
            var materialCode = TextNormalizer.NullIfBlank(row.Material);
            if (materialCode is null)
                Error(OpeningBalanceColumns.Material, ImportErrorCodes.Required, "Material code is required.");
            else if (!materialsByCode.TryGetValue(Material.NormalizeCode(materialCode), out material))
                Error(OpeningBalanceColumns.Material, ImportErrorCodes.UnknownMaterial, "Material not found or inactive.", materialCode);

            Guid? warehouseId = null;
            if (string.IsNullOrWhiteSpace(row.Warehouse))
                Error(OpeningBalanceColumns.Warehouse, ImportErrorCodes.Required, "Warehouse is required.");
            else if (_warehouses.TryMatch(row.Warehouse, out var matched))
                warehouseId = matched;
            else
                Error(OpeningBalanceColumns.Warehouse, ImportErrorCodes.UnknownWarehouse, "Warehouse not found or inactive.", row.Warehouse.Trim());

            Guid? locationId = null;
            var locationResolved = true;
            if (TextNormalizer.NullIfBlank(row.Location) is { } locationCode && warehouseId is { } wh)
            {
                if (locations.TryGetValue((wh, CatalogEntity.NormalizeCode(locationCode)), out var loc))
                    locationId = loc;
                else
                {
                    locationResolved = false;
                    Error(OpeningBalanceColumns.Location, ImportErrorCodes.UnknownLocation, "Location not found in this warehouse.", locationCode);
                }
            }

            var lotNumber = TextNormalizer.NullIfBlank(row.LotNumber);
            var mfg = ParseDate(row.ManufactureDate, OpeningBalanceColumns.ManufactureDate, Error);
            var exp = ParseDate(row.ExpiryDate, OpeningBalanceColumns.ExpiryDate, Error);
            if (mfg is not null && exp is not null && exp < mfg)
                Error(OpeningBalanceColumns.ExpiryDate, ImportErrorCodes.InvalidDate, "Expiry date is before the manufacture date.", row.ExpiryDate);
            if (lotNumber?.Length > Lot.LotNumberMaxLength)
                Error(OpeningBalanceColumns.LotNumber, ImportErrorCodes.TooLong, $"Max {Lot.LotNumberMaxLength} characters.", lotNumber);

            if (material is { TrackLots: true } && lotNumber is null)
                Error(OpeningBalanceColumns.LotNumber, ImportErrorCodes.LotRequired, "Lot number is required for this material.");
            if (material is { TrackLots: false } && (lotNumber is not null || row.ManufactureDate is not null || row.ExpiryDate is not null))
                Error(OpeningBalanceColumns.LotNumber, ImportErrorCodes.LotNotAllowed, "This material is not lot-tracked.", lotNumber);

            if (material is { TrackLots: true } && lotNumber is not null)
            {
                // A blank date means "not given", so it never conflicts; two given dates must be equal.
                var lotKey = (material.Id, lotNumber);
                if (lotDatesInFile.TryGetValue(lotKey, out var first)
                    && (Conflicts(first.Dates.ManufactureDate, mfg) || Conflicts(first.Dates.ExpiryDate, exp)))
                    Error(OpeningBalanceColumns.LotNumber, ImportErrorCodes.LotDatesMismatch, $"Dates differ from row {first.Row}.", lotNumber);
                else if (lotDatesInFile.TryGetValue(lotKey, out first))
                    lotDatesInFile[lotKey] = (first.Row, new LotDates(first.Dates.ManufactureDate ?? mfg, first.Dates.ExpiryDate ?? exp));
                else
                    lotDatesInFile[lotKey] = (row.RowNumber, new LotDates(mfg, exp));

                if (existingLots.TryGetValue((material.Id, lotNumber), out var existing)
                    && (Conflicts(existing.ManufactureDate, mfg) || Conflicts(existing.ExpiryDate, exp)))
                    Error(OpeningBalanceColumns.LotNumber, ImportErrorCodes.LotDatesMismatch, "Lot already exists with different dates.", lotNumber);
            }

            decimal quantity = 0;
            var quantityText = TextNormalizer.NullIfBlank(row.Quantity);
            if (quantityText is null)
                Error(OpeningBalanceColumns.Quantity, ImportErrorCodes.Required, "Quantity is required.");
            else if (!ImportParsing.TryParseDecimal(quantityText, out quantity) || quantity <= 0
                     || quantity > StockDocumentDraftValidator.MaxQuantity || !ImportParsing.HasAtMostThreeDecimals(quantity))
                Error(OpeningBalanceColumns.Quantity, ImportErrorCodes.InvalidNumber, "Must be a number > 0 with at most 3 decimals.", quantityText);

            // Only compare complete keys; a row with an unknown location would collide with location-less rows.
            if (material is not null && warehouseId is not null && locationResolved)
            {
                var key = (material.Id, warehouseId.Value, locationId, lotNumber);
                if (firstRowByKey.TryGetValue(key, out var firstRow))
                    Error(OpeningBalanceColumns.Material, ImportErrorCodes.DuplicateInFile, $"Same material, warehouse, location and lot as row {firstRow}.", materialCode);
                else
                    firstRowByKey[key] = row.RowNumber;
            }

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }
            lines.Add(new OpeningBalanceLine(row.RowNumber, material!.Id, warehouseId!.Value, locationId,
                material.TrackLots ? lotNumber : null, mfg, exp, quantity));
        }

        return (lines, errors);
    }

    private static DateOnly? ParseDate(string? text, string column, Action<string, string, string, string?> error)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (ImportParsing.TryParseDate(text, out var date)) return date;
        error(column, ImportErrorCodes.InvalidDate, "Use dd/MM/yyyy.", text.Trim());
        return null;
    }

    private static bool Conflicts(DateOnly? existing, DateOnly? typed) => existing is not null && typed is not null && existing != typed;
}

public sealed record CreatedDocumentDto(Guid Id, string Number, string WarehouseName, int LineCount);

public sealed record OpeningBalanceImportResult(
    bool Succeeded, int TotalRows, IReadOnlyList<ImportError> Errors, IReadOnlyList<CreatedDocumentDto> Documents);

public interface IOpeningBalanceImportService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates draft receipts (reason OpeningBalance): one per warehouse, split every
    /// <see cref="StockDocument.MaxLines"/> lines. All-or-nothing; nothing is approved.
    /// </summary>
    Task<OpeningBalanceImportResult> ImportAsync(Stream content, DateOnly? documentDate, string? note, CancellationToken ct = default);
}

public sealed class OpeningBalanceImportService(
    IAppDbContext db,
    IExcelImportFile excelFile,
    IStockDocumentService documents,
    BusinessClock clock) : IOpeningBalanceImportService
{
    private static readonly string[] Guide =
    [
        "Hướng dẫn nhập tồn đầu kỳ",
        "",
        "• Nhập dữ liệu ở sheet \"Tồn đầu kỳ\", bắt đầu từ dòng 2. Không đổi thứ tự hoặc tên cột.",
        "• Mã vật tư phải có sẵn trong danh mục. Kho: chọn từ danh sách hoặc gõ mã/tên kho.",
        "• Vị trí: mã vị trí trong kho đó (ví dụ A-01-03). Để trống nếu kho không chia vị trí.",
        "• Vật tư có quản lý lô: bắt buộc Số lô; Ngày sản xuất, Hạn dùng ghi dạng dd/MM/yyyy.",
        "• Vật tư không quản lý lô: để trống Số lô, Ngày sản xuất, Hạn dùng.",
        "• Số lượng > 0, tối đa 3 chữ số thập phân.",
        "• Hệ thống tạo phiếu nhập NHÁP loại \"Tồn đầu kỳ\" (mỗi kho một phiếu). Kiểm tra rồi duyệt phiếu để ghi tồn.",
        "• Nếu có bất kỳ dòng nào lỗi, không phiếu nào được tạo.",
    ];

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct = default) =>
        excelFile.CreateTemplate(new ExcelTemplate(
            "Tồn đầu kỳ",
            OpeningBalanceColumns.All,
            [
                new(OpeningBalanceColumns.Material, "Mã vật tư",
                    await db.Materials.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.Code).Select(m => m.Code).ToListAsync(ct)),
                new(OpeningBalanceColumns.Warehouse, "Kho",
                    await db.Warehouses.AsNoTracking().Where(w => w.IsActive).OrderBy(w => w.Name).Select(w => w.Name).ToListAsync(ct)),
            ],
            Guide));

    public async Task<OpeningBalanceImportResult> ImportAsync(
        Stream content, DateOnly? documentDate, string? note, CancellationToken ct = default)
    {
        var date = documentDate ?? clock.Today;
        if (date > clock.Today)
            throw new ValidationException([new ValidationFailure("documentDate", "Document date cannot be in the future.")]);

        var rows = excelFile.ReadRows(content, OpeningBalanceColumns.All).Select(OpeningBalanceRow.From).ToList();
        if (rows.Count == 0) throw FileError("The file has no data rows.");
        if (rows.Count > ImportLimits.MaxRows) throw FileError($"The file has {rows.Count} rows; the maximum is {ImportLimits.MaxRows}.");

        var codes = rows.Select(r => r.Material?.Trim()).OfType<string>().Select(Material.NormalizeCode).Distinct().ToList();
        var materials = await db.Materials.AsNoTracking().Where(m => m.IsActive && codes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, m => new MaterialRef(m.Id, m.TrackLots), ct);
        var warehouses = await db.Warehouses.AsNoTracking().Where(w => w.IsActive)
            .Select(w => new LookupDto(w.Id, w.Code, w.Name)).ToListAsync(ct);
        var warehouseIds = warehouses.Select(w => w.Id).ToList();
        var locations = await db.Locations.AsNoTracking().Where(l => l.IsActive && warehouseIds.Contains(l.WarehouseId))
            .ToDictionaryAsync(l => (l.WarehouseId, l.Code), l => l.Id, ct);
        var materialIds = materials.Values.Select(m => m.Id).ToList();
        var lotNumbers = rows.Select(r => r.LotNumber?.Trim()).OfType<string>().Distinct().ToList();
        var existingLots = await db.Lots.AsNoTracking()
            .Where(l => materialIds.Contains(l.MaterialId) && lotNumbers.Contains(l.LotNumber))
            .ToDictionaryAsync(l => (l.MaterialId, l.LotNumber), l => new LotDates(l.ManufactureDate, l.ExpiryDate), ct);

        var (lines, errors) = new OpeningBalanceValidator(materials, warehouses, locations, existingLots).Validate(rows);
        if (errors.Count > 0)
            return new OpeningBalanceImportResult(false, rows.Count, errors, []);

        var noteText = TextNormalizer.NullIfBlank(note) ?? "Tồn đầu kỳ (nhập từ Excel)";
        var created = new List<CreatedDocumentDto>();
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        foreach (var byWarehouse in lines.GroupBy(l => l.WarehouseId))
        {
            foreach (var chunk in byWarehouse.Chunk(StockDocument.MaxLines))
            {
                var document = await documents.CreateAsync(new StockDocumentDraft(
                    StockDocumentType.Receipt, date, byWarehouse.Key, null, StockDocumentReason.OpeningBalance, null, null, null,
                    noteText,
                    chunk.Select(l => new StockLineDraft(l.MaterialId, l.LocationId, null, null, l.LotNumber,
                        l.ManufactureDate, l.ExpiryDate, l.Quantity, null)).ToList()), ct);
                created.Add(new CreatedDocumentDto(document.Id, document.Number, document.WarehouseName, document.Lines.Count));
            }
        }
        await tx.CompleteAsync(ct);

        return new OpeningBalanceImportResult(true, rows.Count, [], created);
    }

    private static ValidationException FileError(string message) => new([new ValidationFailure("file", message)]);
}
