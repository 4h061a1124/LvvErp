using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Application.Common.Text;
using LvvErp.Application.Common.Time;
using LvvErp.Domain.Features.Inventory;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Inventory.Documents;

public interface IStockDocumentService
{
    Task<PagedResult<StockDocumentSummaryDto>> ListAsync(StockDocumentType type, StockDocumentListQuery query, CancellationToken ct = default);
    /// <param name="type">When given, a document of another type is reported as not found.</param>
    Task<StockDocumentDto> GetAsync(Guid id, StockDocumentType? type = null, CancellationToken ct = default);
    Task<StockDocumentDto> CreateAsync(StockDocumentDraft draft, CancellationToken ct = default);
    /// <summary>Drafts only; replaces header and all lines.</summary>
    Task<StockDocumentDto> UpdateAsync(Guid id, StockDocumentDraft draft, CancellationToken ct = default);
    /// <summary>Drafts only.</summary>
    Task DeleteAsync(Guid id, StockDocumentType type, CancellationToken ct = default);
    /// <summary>Posts the document to the ledger and balances, all or nothing (409 InsufficientStock).</summary>
    Task<StockDocumentDto> ApproveAsync(Guid id, StockDocumentType type, CancellationToken ct = default);
    /// <summary>Posts reversing entries dated today; needs the stock to still be there.</summary>
    Task<StockDocumentDto> CancelAsync(Guid id, StockDocumentType type, CancelStockDocumentRequest request, CancellationToken ct = default);
}

/// <summary>
/// Every write takes a row lock on the document inside a transaction before looking at its
/// status, so concurrent edit/approve/cancel requests on one document run one after another.
/// </summary>
public sealed class StockDocumentService(
    IAppDbContext db,
    IStockLedger ledger,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    BusinessClock clock,
    IValidator<StockDocumentDraft> draftValidator,
    IValidator<CancelStockDocumentRequest> cancelValidator) : IStockDocumentService
{
    public async Task<PagedResult<StockDocumentSummaryDto>> ListAsync(
        StockDocumentType type, StockDocumentListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, StockDocumentListQuery.MaxPageSize);

        // IgnoreQueryFilters: keep showing the recipient's name after the employee is deleted.
        var docs = db.StockDocuments.AsNoTracking().IgnoreQueryFilters().Where(d => d.Type == type);

        if (query.Status is { } status) docs = docs.Where(d => d.Status == status);
        if (query.WarehouseId is { } warehouseId) docs = docs.Where(d => d.WarehouseId == warehouseId || d.ToWarehouseId == warehouseId);
        if (query.From is { } from) docs = docs.Where(d => d.DocumentDate >= from);
        if (query.To is { } to) docs = docs.Where(d => d.DocumentDate <= to);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            docs = docs.Where(d =>
                d.Number.ToLower().Contains(term)
                || (d.Partner != null && AppDbFunctions.Unaccent(d.Partner.ToLower()).Contains(AppDbFunctions.Unaccent(term)))
                || (d.Note != null && AppDbFunctions.Unaccent(d.Note.ToLower()).Contains(AppDbFunctions.Unaccent(term)))
                || (d.Employee != null && AppDbFunctions.Unaccent(d.Employee.FullName.ToLower()).Contains(AppDbFunctions.Unaccent(term))));
        }

        var total = await docs.CountAsync(ct);
        var descending = query.Sort is null || query.Sort.StartsWith('-');
        var ordered = query.Sort?.TrimStart('-').ToLowerInvariant() == "number"
            ? (descending ? docs.OrderByDescending(d => d.Number) : docs.OrderBy(d => d.Number))
            : (descending
                ? docs.OrderByDescending(d => d.DocumentDate).ThenByDescending(d => d.Number)
                : docs.OrderBy(d => d.DocumentDate).ThenBy(d => d.Number));

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new StockDocumentSummaryDto(
                d.Id, d.Number, d.Type, d.Status, d.DocumentDate, d.Warehouse.Name,
                d.ToWarehouse != null ? d.ToWarehouse.Name : null, d.Reason, d.Partner,
                d.Department != null ? d.Department.Name : null, d.Employee != null ? d.Employee.FullName : null,
                d.Lines.Count, d.CreatedBy, d.CreatedAt, d.ApprovedAt))
            .ToListAsync(ct);

        return new PagedResult<StockDocumentSummaryDto>(items, page, pageSize, total);
    }

    public async Task<StockDocumentDto> GetAsync(Guid id, StockDocumentType? type = null, CancellationToken ct = default) =>
        await db.StockDocuments.AsNoTracking().IgnoreQueryFilters()
            .Where(d => d.Id == id && (type == null || d.Type == type))
            .Select(d => new StockDocumentDto(
                d.Id, d.Number, d.Type, d.Status, d.DocumentDate, d.WarehouseId, d.Warehouse.Name,
                d.ToWarehouseId, d.ToWarehouse != null ? d.ToWarehouse.Name : null, d.Reason, d.Partner,
                d.DepartmentId, d.Department != null ? d.Department.Name : null,
                d.EmployeeId, d.Employee != null ? d.Employee.FullName : null,
                d.Note, d.CreatedAt, d.CreatedBy, d.ApprovedAt, d.ApprovedBy, d.CancelledAt, d.CancelledBy, d.CancelReason,
                d.Lines.OrderBy(l => l.LineNo).Select(l => new StockDocumentLineDto(
                    l.Id, l.LineNo, l.MaterialId, l.Material.Code, l.Material.Name, l.Material.Unit.Name, l.Material.TrackLots,
                    l.LocationId, l.Location != null ? l.Location.Code : null,
                    l.ToLocationId, l.ToLocation != null ? l.ToLocation.Code : null,
                    l.LotId,
                    l.Lot != null ? l.Lot.LotNumber : l.LotNumber,
                    l.Lot != null ? l.Lot.ManufactureDate : l.ManufactureDate,
                    l.Lot != null ? l.Lot.ExpiryDate : l.ExpiryDate,
                    l.Quantity, l.Note)).ToList()))
            .SingleOrDefaultAsync(ct)
        ?? throw NotFoundException.For<StockDocument>(id);

    public async Task<StockDocumentDto> CreateAsync(StockDocumentDraft draft, CancellationToken ct = default)
    {
        await draftValidator.ValidateAndThrowAsync(draft, ct);
        await ValidateReferencesAsync(draft, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var sequence = await ledger.NextDocumentNumberAsync(
            StockDocument.NumberPrefix(draft.Type), draft.DocumentDate.Year, ct);
        var document = new StockDocument
        {
            Type = draft.Type,
            Number = StockDocument.FormatNumber(draft.Type, draft.DocumentDate.Year, sequence),
        };
        Apply(document, draft);
        db.StockDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(document.Id, ct: ct);
    }

    public async Task<StockDocumentDto> UpdateAsync(Guid id, StockDocumentDraft draft, CancellationToken ct = default)
    {
        await draftValidator.ValidateAndThrowAsync(draft, ct);
        await ValidateReferencesAsync(draft, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var document = await LockAndLoadAsync(id, draft.Type, ct);
        EnsureStatus(document, StockDocumentStatus.Draft);

        db.StockDocumentLines.RemoveRange(document.Lines);
        document.Lines.Clear();
        Apply(document, draft);
        document.UpdatedAt = timeProvider.GetUtcNow(); // header may be unchanged; still record the edit
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct: ct);
    }

    public async Task DeleteAsync(Guid id, StockDocumentType type, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var document = await LockAndLoadAsync(id, type, ct);
        EnsureStatus(document, StockDocumentStatus.Draft);

        db.StockDocuments.Remove(document);
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);
    }

    public async Task<StockDocumentDto> ApproveAsync(Guid id, StockDocumentType type, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var document = await LockAndLoadAsync(id, type, ct);
        EnsureStatus(document, StockDocumentStatus.Draft);

        // Catalog data may have changed since the draft was saved (deactivated material, ...).
        await ValidateReferencesAsync(ToDraft(document), ct);

        if (document.Type == StockDocumentType.Receipt)
        {
            await ResolveReceiptLotsAsync(document, ct);
            await db.SaveChangesAsync(ct); // new lots must exist before balances reference them
        }

        var movements = StockPosting.ForApproval(document);
        await ApplyMovementsAsync(movements, ct);
        AddLedgerRows(document, movements, document.DocumentDate, isReversal: false);

        document.Status = StockDocumentStatus.Approved;
        document.ApprovedAt = timeProvider.GetUtcNow();
        document.ApprovedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct: ct);
    }

    public async Task<StockDocumentDto> CancelAsync(
        Guid id, StockDocumentType type, CancelStockDocumentRequest request, CancellationToken ct = default)
    {
        await cancelValidator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var document = await LockAndLoadAsync(id, type, ct);
        if (document.Status != StockDocumentStatus.Approved)
            throw new ConflictException("Only approved documents can be cancelled.", ErrorCodes.DocumentNotApproved);

        var posted = await db.StockTransactions.Where(t => t.DocumentId == id).ToListAsync(ct);
        var movements = StockPosting.ForCancellation(posted, document.Lines.ToDictionary(l => l.Id, l => l.LineNo));
        await ApplyMovementsAsync(movements, ct);
        AddLedgerRows(document, movements, clock.Today, isReversal: true);

        document.Status = StockDocumentStatus.Cancelled;
        document.CancelledAt = timeProvider.GetUtcNow();
        document.CancelledBy = currentUser.UserName;
        document.CancelReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct: ct);
    }

    private async Task<StockDocument> LockAndLoadAsync(Guid id, StockDocumentType type, CancellationToken ct)
    {
        await ledger.LockDocumentAsync(id, ct);
        return await db.StockDocuments.Include(d => d.Lines).SingleOrDefaultAsync(d => d.Id == id && d.Type == type, ct)
            ?? throw NotFoundException.For<StockDocument>(id);
    }

    private static void EnsureStatus(StockDocument document, StockDocumentStatus expected)
    {
        if (document.Status != expected)
            throw new ConflictException($"Document {document.Number} is {document.Status} and can no longer be changed.",
                ErrorCodes.DocumentNotDraft);
    }

    private static void Apply(StockDocument document, StockDocumentDraft draft)
    {
        document.DocumentDate = draft.DocumentDate;
        document.WarehouseId = draft.WarehouseId;
        document.ToWarehouseId = draft.ToWarehouseId;
        document.Reason = draft.Reason;
        document.Partner = TextNormalizer.NullIfBlank(draft.Partner);
        document.DepartmentId = draft.DepartmentId;
        document.EmployeeId = draft.EmployeeId;
        document.Note = TextNormalizer.NullIfBlank(draft.Note);
        document.Lines = draft.Lines.Select((l, i) => new StockDocumentLine
        {
            LineNo = i + 1,
            MaterialId = l.MaterialId,
            LocationId = l.LocationId,
            ToLocationId = l.ToLocationId,
            LotId = l.LotId,
            LotNumber = TextNormalizer.NullIfBlank(l.LotNumber),
            ManufactureDate = l.ManufactureDate,
            ExpiryDate = l.ExpiryDate,
            Quantity = l.Quantity,
            Note = TextNormalizer.NullIfBlank(l.Note),
        }).ToList();
    }

    private static StockDocumentDraft ToDraft(StockDocument d) => new(
        d.Type, d.DocumentDate, d.WarehouseId, d.ToWarehouseId, d.Reason, d.Partner, d.DepartmentId, d.EmployeeId, d.Note,
        d.Lines.OrderBy(l => l.LineNo).Select(l => new StockLineDraft(
            l.MaterialId, l.LocationId, l.ToLocationId, l.LotId, l.LotNumber, l.ManufactureDate, l.ExpiryDate,
            l.Quantity, l.Note)).ToList());

    /// <summary>Rules that need the database. Errors use the request's property paths ("Lines[2].LotId").</summary>
    private async Task ValidateReferencesAsync(StockDocumentDraft draft, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        void Fail(string property, string message) => failures.Add(new ValidationFailure(property, message));

        var warehouseIds = new[] { draft.WarehouseId, draft.ToWarehouseId }.OfType<Guid>().Distinct().ToList();
        var activeWarehouses = await db.Warehouses
            .Where(w => warehouseIds.Contains(w.Id) && w.IsActive).Select(w => w.Id).ToListAsync(ct);
        if (!activeWarehouses.Contains(draft.WarehouseId))
            Fail(nameof(draft.WarehouseId), "Warehouse does not exist or is inactive.");
        if (draft.ToWarehouseId is { } toWarehouseId && !activeWarehouses.Contains(toWarehouseId))
            Fail(nameof(draft.ToWarehouseId), "Warehouse does not exist or is inactive.");

        if (draft.DepartmentId is { } departmentId && !await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            Fail(nameof(draft.DepartmentId), "Department does not exist.");
        if (draft.EmployeeId is { } employeeId && !await db.Employees.AnyAsync(e => e.Id == employeeId, ct))
            Fail(nameof(draft.EmployeeId), "Employee does not exist.");

        var materialIds = draft.Lines.Select(l => l.MaterialId).Distinct().ToList();
        var materials = await db.Materials.Where(m => materialIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => new { m.TrackLots, m.IsActive }, ct);

        var locationIds = draft.Lines.SelectMany(l => new[] { l.LocationId, l.ToLocationId }).OfType<Guid>().Distinct().ToList();
        var locations = await db.Locations.Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new { l.WarehouseId, l.IsActive }, ct);

        var lotIds = draft.Lines.Select(l => l.LotId).OfType<Guid>().Distinct().ToList();
        var lots = await db.Lots.Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.MaterialId, ct);

        var lotNumbers = draft.Lines.Select(l => l.LotNumber?.Trim()).OfType<string>().Distinct().ToList();
        var existingLots = draft.Type == StockDocumentType.Receipt && lotNumbers.Count > 0
            ? await db.Lots.Where(l => materialIds.Contains(l.MaterialId) && lotNumbers.Contains(l.LotNumber))
                .ToDictionaryAsync(l => (l.MaterialId, l.LotNumber), ct)
            : [];

        void CheckLocation(Guid? locationId, Guid? warehouseId, string property)
        {
            if (locationId is null) return;
            if (!locations.TryGetValue(locationId.Value, out var location) || !location.IsActive)
                Fail(property, "Location does not exist or is inactive.");
            else if (location.WarehouseId != warehouseId)
                Fail(property, "Location belongs to another warehouse.");
        }

        for (var i = 0; i < draft.Lines.Count; i++)
        {
            var line = draft.Lines[i];
            var path = $"Lines[{i}]";

            if (!materials.TryGetValue(line.MaterialId, out var material) || !material.IsActive)
            {
                Fail($"{path}.MaterialId", "Material does not exist or is inactive.");
                continue;
            }

            CheckLocation(line.LocationId, draft.WarehouseId, $"{path}.LocationId");
            if (draft.Type == StockDocumentType.Transfer)
                CheckLocation(line.ToLocationId, draft.ToWarehouseId, $"{path}.ToLocationId");
            else if (line.ToLocationId is not null)
                Fail($"{path}.ToLocationId", "Only transfers have a destination location.");

            var hasLotNumber = !string.IsNullOrWhiteSpace(line.LotNumber);
            var hasLotDates = line.ManufactureDate is not null || line.ExpiryDate is not null;

            if (draft.Type == StockDocumentType.Receipt)
            {
                if (line.LotId is not null)
                    Fail($"{path}.LotId", "Receipts name the lot by LotNumber.");
                if (material.TrackLots && !hasLotNumber)
                    Fail($"{path}.LotNumber", "Lot number is required for this material.");
                if (!material.TrackLots && (hasLotNumber || hasLotDates))
                    Fail($"{path}.LotNumber", "This material is not lot-tracked.");
                if (material.TrackLots && hasLotNumber
                    && existingLots.TryGetValue((line.MaterialId, line.LotNumber!.Trim()), out var existing)
                    && (Differs(existing.ManufactureDate, line.ManufactureDate) || Differs(existing.ExpiryDate, line.ExpiryDate)))
                {
                    Fail($"{path}.LotNumber", $"Lot {existing.LotNumber} already exists with different dates.");
                }
            }
            else
            {
                if (hasLotNumber || hasLotDates)
                    Fail($"{path}.LotNumber", "Issues and transfers pick an existing lot by LotId.");
                if (material.TrackLots && line.LotId is null)
                    Fail($"{path}.LotId", "Lot is required for this material.");
                if (!material.TrackLots && line.LotId is not null)
                    Fail($"{path}.LotId", "This material is not lot-tracked.");
                if (line.LotId is { } lotId && (!lots.TryGetValue(lotId, out var lotMaterialId) || lotMaterialId != line.MaterialId))
                    Fail($"{path}.LotId", "Lot does not belong to this material.");
            }
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        static bool Differs(DateOnly? existing, DateOnly? typed) => existing is not null && typed is not null && existing != typed;
    }

    /// <summary>Finds or creates the lot named on each receipt line and links it.</summary>
    private async Task ResolveReceiptLotsAsync(StockDocument document, CancellationToken ct)
    {
        var resolved = new Dictionary<(Guid, string), Lot>();
        foreach (var line in document.Lines.Where(l => l.LotNumber is not null).OrderBy(l => l.LineNo))
        {
            var key = (line.MaterialId, line.LotNumber!);
            if (!resolved.TryGetValue(key, out var lot))
            {
                lot = await db.Lots.SingleOrDefaultAsync(l => l.MaterialId == line.MaterialId && l.LotNumber == line.LotNumber, ct);
                if (lot is null)
                {
                    lot = new Lot { MaterialId = line.MaterialId, LotNumber = line.LotNumber! };
                    db.Lots.Add(lot);
                }
                resolved[key] = lot;
            }
            lot.ManufactureDate ??= line.ManufactureDate;
            lot.ExpiryDate ??= line.ExpiryDate;
            line.LotId = lot.Id;
        }
    }

    /// <summary>Applies movements in lock order; on any shortage throws 409 with every short line.</summary>
    private async Task ApplyMovementsAsync(IReadOnlyList<StockMovement> movements, CancellationToken ct)
    {
        var shortages = new List<StockMovement>();
        foreach (var movement in StockPosting.InLockOrder(movements))
        {
            if (movement.Quantity > 0)
                await ledger.IncreaseAsync(movement.Key, movement.Quantity, ct);
            else if (!await ledger.TryDecreaseAsync(movement.Key, -movement.Quantity, ct))
                shortages.Add(movement);
        }
        if (shortages.Count == 0) return;

        var details = new List<StockShortage>();
        foreach (var s in shortages.OrderBy(s => s.LineNo))
        {
            var key = s.Key;
            var available = await db.StockBalances.AsNoTracking()
                .Where(b => b.MaterialId == key.MaterialId && b.WarehouseId == key.WarehouseId
                            && b.LocationId == key.LocationId && b.LotId == key.LotId)
                .Select(b => (decimal?)b.Quantity).SingleOrDefaultAsync(ct) ?? 0;
            var materialCode = await db.Materials.Where(m => m.Id == key.MaterialId).Select(m => m.Code).SingleAsync(ct);
            var lotNumber = key.LotId is null
                ? null
                : await db.Lots.Where(l => l.Id == key.LotId).Select(l => l.LotNumber).SingleAsync(ct);
            details.Add(new StockShortage(s.LineNo, materialCode, lotNumber, -s.Quantity, available));
        }

        throw new ConflictException(
            $"Not enough stock: {string.Join("; ", details.Select(d => $"line {d.LineNo} {d.MaterialCode} needs {d.Requested}, has {d.Available}"))}.",
            ErrorCodes.InsufficientStock,
            details);
    }

    private void AddLedgerRows(StockDocument document, IEnumerable<StockMovement> movements, DateOnly date, bool isReversal)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var m in movements)
        {
            db.StockTransactions.Add(new StockTransaction
            {
                DocumentId = document.Id,
                DocumentLineId = m.LineId,
                MaterialId = m.Key.MaterialId,
                WarehouseId = m.Key.WarehouseId,
                LocationId = m.Key.LocationId,
                LotId = m.Key.LotId,
                Quantity = m.Quantity,
                TransactionDate = date,
                IsReversal = isReversal,
                CreatedAt = now,
                CreatedBy = currentUser.UserName,
            });
        }
    }
}
