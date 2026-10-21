using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Text;
using LvvErp.Application.Common.Validation;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Import;

public static class MaterialImportColumns
{
    public const string Code = "Code";
    public const string Name = "Name";
    public const string Specification = "Specification";
    public const string Unit = "Unit";
    public const string Group = "Group";
    public const string MinStock = "MinStock";
    public const string TrackLots = "TrackLots";

    public static readonly IReadOnlyList<ExcelColumn> All =
    [
        new(Code, "Mã vật tư *", 16, IsText: true),
        new(Name, "Tên vật tư *", 34),
        new(Specification, "Quy cách", 30),
        new(Unit, "ĐVT *", 12, ListName: Unit),
        new(Group, "Nhóm *", 24, ListName: Group),
        new(MinStock, "Tồn tối thiểu", 14),
        new(TrackLots, "Quản lý lô", 12, ListName: TrackLots),
    ];
}

public sealed record MaterialImportRow(
    int RowNumber, string? Code, string? Name, string? Specification, string? Unit, string? Group, string? MinStock, string? TrackLots)
{
    public static MaterialImportRow From(ExcelRow r) => new(
        r.RowNumber, r[MaterialImportColumns.Code], r[MaterialImportColumns.Name], r[MaterialImportColumns.Specification],
        r[MaterialImportColumns.Unit], r[MaterialImportColumns.Group], r[MaterialImportColumns.MinStock], r[MaterialImportColumns.TrackLots]);
}

/// <summary>Blank optional cells are null: keep the current value on update, default on create.</summary>
public sealed record ValidatedMaterialRow(
    int RowNumber, string Code, string Name, string? Specification, Guid UnitId, Guid GroupId,
    decimal? MinStock, bool? TrackLots, bool IsUpdate);

/// <param name="TrackLotsLocked">The material already appears on stock documents.</param>
public sealed record ExistingMaterial(bool TrackLots, bool TrackLotsLocked);

/// <summary>Pure row validation for the material import (no database access).</summary>
public sealed class MaterialImportValidator(
    IEnumerable<LookupDto> units,
    IEnumerable<LookupDto> groups,
    IReadOnlyDictionary<string, ExistingMaterial> existingByCode,
    bool updateExisting)
{
    private readonly LookupMatcher _units = new(units);
    private readonly LookupMatcher _groups = new(groups);

    public (IReadOnlyList<ValidatedMaterialRow> Rows, IReadOnlyList<ImportError> Errors) Validate(IReadOnlyList<MaterialImportRow> rows)
    {
        var valid = new List<ValidatedMaterialRow>();
        var errors = new List<ImportError>();
        var firstRowByCode = new Dictionary<string, int>();

        foreach (var row in rows)
        {
            var rowErrors = new List<ImportError>();
            void Error(string column, string code, string message, string? value = null) =>
                rowErrors.Add(new ImportError(row.RowNumber, column, code, message, value));

            var code = row.Code?.Trim() ?? string.Empty;
            ExistingMaterial? existing = null;
            if (code.Length == 0)
                Error(MaterialImportColumns.Code, ImportErrorCodes.Required, "Material code is required.");
            else if (code.Length > Material.CodeMaxLength)
                Error(MaterialImportColumns.Code, ImportErrorCodes.TooLong, $"Max {Material.CodeMaxLength} characters.", code);
            else if (!ValidationRules.CodePattern().IsMatch(code))
                Error(MaterialImportColumns.Code, ImportErrorCodes.InvalidFormat, "Only letters, digits, '.', '_' and '-'.", code);
            else
            {
                code = Material.NormalizeCode(code);
                if (firstRowByCode.TryGetValue(code, out var firstRow))
                    Error(MaterialImportColumns.Code, ImportErrorCodes.DuplicateInFile, $"Same code as row {firstRow}.", code);
                else
                    firstRowByCode[code] = row.RowNumber;

                existingByCode.TryGetValue(code, out existing);
                if (existing is not null && !updateExisting)
                    Error(MaterialImportColumns.Code, ImportErrorCodes.AlreadyExists, "Material code already exists.", code);
            }

            var name = row.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
                Error(MaterialImportColumns.Name, ImportErrorCodes.Required, "Name is required.");
            else if (name.Length > Material.NameMaxLength)
                Error(MaterialImportColumns.Name, ImportErrorCodes.TooLong, $"Max {Material.NameMaxLength} characters.", name);

            var specification = TextNormalizer.NullIfBlank(row.Specification);
            if (specification?.Length > Material.SpecificationMaxLength)
                Error(MaterialImportColumns.Specification, ImportErrorCodes.TooLong, $"Max {Material.SpecificationMaxLength} characters.", specification);

            var unitId = Match(_units, row.Unit, MaterialImportColumns.Unit, ImportErrorCodes.UnknownUnit, Error);
            var groupId = Match(_groups, row.Group, MaterialImportColumns.Group, ImportErrorCodes.UnknownGroup, Error);

            decimal? minStock = null;
            if (TextNormalizer.NullIfBlank(row.MinStock) is { } minText)
            {
                if (ImportParsing.TryParseDecimal(minText, out var parsed) && parsed >= 0
                    && parsed <= SaveMaterialRequestValidator.MaxQuantity && ImportParsing.HasAtMostThreeDecimals(parsed))
                    minStock = parsed;
                else
                    Error(MaterialImportColumns.MinStock, ImportErrorCodes.InvalidNumber, "Must be a number ≥ 0 with at most 3 decimals.", minText);
            }

            bool? trackLots = null;
            var trackText = TextNormalizer.NullIfBlank(row.TrackLots);
            if (trackText is not null)
            {
                if (ImportParsing.TryParseBool(trackText, out var parsed))
                    trackLots = parsed;
                else
                    Error(MaterialImportColumns.TrackLots, ImportErrorCodes.InvalidFormat, "Use Có or Không.", trackText);
            }
            if (existing is { TrackLotsLocked: true } && trackLots is { } wanted && wanted != existing.TrackLots)
                Error(MaterialImportColumns.TrackLots, ImportErrorCodes.TrackLotsLocked,
                    "Lot tracking can't change once the material is used in stock documents.", trackText);

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                continue;
            }
            valid.Add(new ValidatedMaterialRow(row.RowNumber, code, name, specification, unitId!.Value, groupId!.Value,
                minStock, trackLots, IsUpdate: existing is not null));
        }

        return (valid, errors);
    }

    private static Guid? Match(LookupMatcher lookup, string? text, string column, string unknownCode,
        Action<string, string, string, string?> error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            error(column, ImportErrorCodes.Required, $"{column} is required.", null);
            return null;
        }
        if (lookup.TryMatch(text, out var id)) return id;
        error(column, unknownCode, $"{column} not found or inactive (match by code or name).", text.Trim());
        return null;
    }
}

public interface IMaterialImportService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken ct = default);

    /// <summary>
    /// All-or-nothing. With <paramref name="updateExisting"/>, existing codes are updated; blank
    /// Quy cách / Tồn tối thiểu / Quản lý lô cells keep the current values.
    /// </summary>
    Task<ImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default);
}

public sealed class MaterialImportService(IAppDbContext db, IExcelImportFile excelFile) : IMaterialImportService
{
    private static readonly string[] Guide =
    [
        "Hướng dẫn nhập danh mục vật tư",
        "",
        "• Nhập dữ liệu ở sheet \"Vật tư\", bắt đầu từ dòng 2. Không đổi thứ tự hoặc tên cột.",
        "• Cột có dấu * là bắt buộc. Mã vật tư chỉ gồm chữ không dấu, số và . _ -",
        "• ĐVT, Nhóm: chọn từ danh sách hoặc gõ mã/tên (không phân biệt hoa thường, có dấu hay không).",
        "• Tồn tối thiểu: số ≥ 0, tối đa 3 chữ số thập phân (ví dụ 12,5). Để trống = 0.",
        "• Quản lý lô: Có / Không. Để trống = Không. Không đổi được khi vật tư đã có trên phiếu kho.",
        "• Nếu có bất kỳ dòng nào lỗi, toàn bộ file sẽ không được lưu.",
        "• Khi bật \"Cập nhật nếu trùng mã\": ô Quy cách, Tồn tối thiểu, Quản lý lô để trống sẽ giữ nguyên giá trị hiện tại.",
    ];

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct = default) =>
        excelFile.CreateTemplate(new ExcelTemplate(
            "Vật tư",
            MaterialImportColumns.All,
            [
                new(MaterialImportColumns.Unit, "Đơn vị tính", (await ActiveUnitsAsync(ct)).Select(u => u.Name).ToList()),
                new(MaterialImportColumns.Group, "Nhóm vật tư", (await ActiveGroupsAsync(ct)).Select(g => g.Name).ToList()),
                new(MaterialImportColumns.TrackLots, "Quản lý lô", ["Có", "Không"]),
            ],
            Guide));

    public async Task<ImportResult> ImportAsync(Stream content, bool updateExisting, CancellationToken ct = default)
    {
        var rows = excelFile.ReadRows(content, MaterialImportColumns.All).Select(MaterialImportRow.From).ToList();
        if (rows.Count == 0) throw FileError("The file has no data rows.");
        if (rows.Count > ImportLimits.MaxRows) throw FileError($"The file has {rows.Count} rows; the maximum is {ImportLimits.MaxRows}.");

        var codes = rows.Select(r => r.Code?.Trim()).OfType<string>().Where(c => c.Length > 0)
            .Select(Material.NormalizeCode).Distinct().ToList();
        var existing = await db.Materials.Where(m => codes.Contains(m.Code)).ToDictionaryAsync(m => m.Code, ct);
        var existingIds = existing.Values.Select(m => m.Id).ToList();
        var used = (await db.StockDocumentLines.Where(l => existingIds.Contains(l.MaterialId))
            .Select(l => l.MaterialId).Distinct().ToListAsync(ct)).ToHashSet();

        var validator = new MaterialImportValidator(
            await ActiveUnitsAsync(ct),
            await ActiveGroupsAsync(ct),
            existing.ToDictionary(kv => kv.Key, kv => new ExistingMaterial(kv.Value.TrackLots, used.Contains(kv.Value.Id))),
            updateExisting);
        var (validRows, errors) = validator.Validate(rows);
        if (errors.Count > 0)
            return new ImportResult(false, rows.Count, 0, 0, errors);

        foreach (var row in validRows)
        {
            if (row.IsUpdate)
            {
                var material = existing[row.Code];
                material.Name = row.Name;
                material.Specification = row.Specification ?? material.Specification;
                material.UnitId = row.UnitId;
                material.GroupId = row.GroupId;
                material.MinStock = row.MinStock ?? material.MinStock;
                material.TrackLots = row.TrackLots ?? material.TrackLots;
            }
            else
            {
                db.Materials.Add(new Material
                {
                    Code = row.Code,
                    Name = row.Name,
                    Specification = row.Specification,
                    UnitId = row.UnitId,
                    GroupId = row.GroupId,
                    MinStock = row.MinStock ?? 0,
                    TrackLots = row.TrackLots ?? false,
                });
            }
        }
        await db.SaveChangesAsync(ct); // one SaveChanges = one transaction

        var updated = validRows.Count(r => r.IsUpdate);
        return new ImportResult(true, rows.Count, validRows.Count - updated, updated, []);
    }

    private Task<List<LookupDto>> ActiveUnitsAsync(CancellationToken ct) =>
        db.Units.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).Select(u => new LookupDto(u.Id, u.Code, u.Name)).ToListAsync(ct);

    private Task<List<LookupDto>> ActiveGroupsAsync(CancellationToken ct) =>
        db.MaterialGroups.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.Name).Select(g => new LookupDto(g.Id, g.Code, g.Name)).ToListAsync(ct);

    private static ValidationException FileError(string message) => new([new ValidationFailure("file", message)]);
}
