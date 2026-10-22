using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Common.Text;
using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Orders;

public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderSummaryDto>> ListAsync(PurchaseOrderListQuery query, CancellationToken ct = default);
    Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken ct = default);
    /// <summary>Draft or Rejected (goes back to Draft); replaces header and lines.</summary>
    Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderRequest request, CancellationToken ct = default);
    /// <summary>Drafts only; releases the request quantities.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Fixes the approval level from the total and the director threshold.</summary>
    Task<PurchaseOrderDto> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseOrderDto> RejectAsync(Guid id, ReasonRequest request, CancellationToken ct = default);
    /// <summary>Before anything is received; releases the request quantities.</summary>
    Task<PurchaseOrderDto> CancelAsync(Guid id, ReasonRequest request, CancellationToken ct = default);
    /// <summary>Ends a partially received order; the rest will not be delivered.</summary>
    Task<PurchaseOrderDto> CloseAsync(Guid id, ReasonRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<LastPriceDto>> LastPricesAsync(IReadOnlyList<Guid> materialIds, Guid? supplierId, CancellationToken ct = default);
    Task<PagedResult<PriceHistoryDto>> PriceHistoryAsync(PriceHistoryQuery query, CancellationToken ct = default);
}

/// <summary>
/// Lock order: the order row first, then the purchase requests it draws from (in id order). Request
/// services only ever lock requests, so the two can't deadlock.
/// </summary>
public sealed class PurchaseOrderService(
    IAppDbContext db,
    IStockLedger ledger,
    IRowLocker locker,
    IUserScopeProvider scopeProvider,
    TimeProvider timeProvider,
    BusinessClock clock,
    IValidator<SavePurchaseOrderRequest> validator,
    IValidator<ReasonRequest> reasonValidator) : IPurchaseOrderService
{
    /// <summary>Statuses whose prices count as agreed (price history, last price).</summary>
    private static readonly PurchaseOrderStatus[] PricedStatuses =
    [
        PurchaseOrderStatus.Approved, PurchaseOrderStatus.PartiallyReceived, PurchaseOrderStatus.Received, PurchaseOrderStatus.Closed,
    ];

    public async Task<PagedResult<PurchaseOrderSummaryDto>> ListAsync(PurchaseOrderListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, PurchaseOrderListQuery.MaxPageSize);
        var today = clock.Today;

        var orders = db.PurchaseOrders.AsNoTracking();
        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);
        if (query.SupplierId is { } supplierId) orders = orders.Where(o => o.SupplierId == supplierId);
        if (query.OverdueOnly) orders = orders.Where(Overdue(today));
        if (query.From is { } from) orders = orders.Where(o => o.OrderDate >= from);
        if (query.To is { } to) orders = orders.Where(o => o.OrderDate <= to);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            orders = orders.Where(o =>
                o.Number.ToLower().Contains(term)
                || o.Supplier.Code.ToLower().Contains(term)
                || AppDbFunctions.Unaccent(o.Supplier.Name.ToLower()).Contains(AppDbFunctions.Unaccent(term)));
        }

        var total = await orders.CountAsync(ct);
        var descending = query.Sort is null || query.Sort.StartsWith('-');
        var ordered = query.Sort?.TrimStart('-').ToLowerInvariant() switch
        {
            "number" => descending ? orders.OrderByDescending(o => o.Number) : orders.OrderBy(o => o.Number),
            "total" => descending ? orders.OrderByDescending(o => o.Total).ThenByDescending(o => o.Number)
                : orders.OrderBy(o => o.Total).ThenBy(o => o.Number),
            "expected" => descending ? orders.OrderByDescending(o => o.ExpectedDate).ThenByDescending(o => o.Number)
                : orders.OrderBy(o => o.ExpectedDate).ThenBy(o => o.Number),
            _ => descending ? orders.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Number)
                : orders.OrderBy(o => o.OrderDate).ThenBy(o => o.Number),
        };

        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Summary(today)).ToListAsync(ct);
        return new PagedResult<PurchaseOrderSummaryDto>(items, page, pageSize, total);
    }

    internal static Expression<Func<PurchaseOrder, bool>> Overdue(DateOnly today) =>
        o => o.ExpectedDate != null && o.ExpectedDate < today
             && (o.Status == PurchaseOrderStatus.Approved || o.Status == PurchaseOrderStatus.PartiallyReceived);

    internal static Expression<Func<PurchaseOrder, PurchaseOrderSummaryDto>> Summary(DateOnly today) =>
        o => new PurchaseOrderSummaryDto(
            o.Id, o.Number, o.Status, o.OrderDate, o.ExpectedDate, o.SupplierId, o.Supplier.Name, o.Warehouse.Name,
            o.VatRate, o.Total, o.Lines.Count, o.ApprovalLevel,
            o.ExpectedDate != null && o.ExpectedDate < today
                && (o.Status == PurchaseOrderStatus.Approved || o.Status == PurchaseOrderStatus.PartiallyReceived),
            o.CreatedBy, o.CreatedAt, o.SubmittedAt);

    public async Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var today = clock.Today;
        var dto = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new PurchaseOrderDto(
                o.Id, o.Number, o.Status, o.OrderDate, o.ExpectedDate,
                new OrderSupplierDto(o.Supplier.Id, o.Supplier.Code, o.Supplier.Name, o.Supplier.TaxCode, o.Supplier.Address,
                    o.Supplier.ContactName, o.Supplier.Phone, o.Supplier.Email),
                o.WarehouseId, o.Warehouse.Name, o.PaymentTermDays, o.VatRate, o.Note, o.Subtotal, o.VatAmount, o.Total,
                o.ApprovalLevel,
                o.ExpectedDate != null && o.ExpectedDate < today
                    && (o.Status == PurchaseOrderStatus.Approved || o.Status == PurchaseOrderStatus.PartiallyReceived),
                o.CreatedAt, o.CreatedBy, o.SubmittedAt, o.SubmittedBy, o.ApprovedAt, o.ApprovedBy,
                o.RejectedAt, o.RejectedBy, o.RejectReason, o.ClosedAt, o.ClosedBy, o.CloseReason,
                o.CancelledAt, o.CancelledBy, o.CancelReason,
                o.Lines.OrderBy(l => l.LineNo).Select(l => new PurchaseOrderLineDto(
                    l.Id, l.LineNo, l.MaterialId, l.Material.Code, l.Material.Name, l.Material.Specification, l.Material.Unit.Name,
                    l.Quantity, l.UnitPrice, l.Amount, l.Note, l.OverOrderReason,
                    db.StockDocumentLines
                        .Where(r => r.PurchaseOrderLineId == l.Id && r.Document.Status == StockDocumentStatus.Approved)
                        .Sum(r => (decimal?)r.Quantity) ?? 0,
                    l.Material.ReceiptTolerancePercent,
                    l.Material.TrackLots,
                    l.Sources.OrderBy(s => s.RequestLine.Request.Number).ThenBy(s => s.RequestLine.LineNo)
                        .Select(s => new OrderLineSourceDto(
                            s.RequestLineId, s.RequestLine.RequestId, s.RequestLine.Request.Number, s.RequestLine.LineNo, s.Quantity))
                        .ToList())).ToList(),
                db.StockDocuments.Where(d => d.PurchaseOrderId == o.Id)
                    .OrderBy(d => d.DocumentDate).ThenBy(d => d.Number)
                    .Select(d => new OrderReceiptDto(d.Id, d.Number, d.Status, d.DocumentDate))
                    .ToList(),
                new PurchaseOrderActions(false, false, false, false, false, false)))
            .SingleOrDefaultAsync(ct)
            ?? throw NotFoundException.For<PurchaseOrder>(id);

        var scope = await scopeProvider.GetAsync(ct);
        var canManage = scope.Can(Permissions.PurchasingManageOrders);
        var canApprove = dto.Status == PurchaseOrderStatus.Submitted && dto.ApprovalLevel is { } level
                         && PurchasingRules.CanApproveOrder(scope, level, await PurchasingDepartmentIdAsync(ct), dto.CreatedBy, dto.SubmittedBy);
        return dto with
        {
            Actions = new PurchaseOrderActions(
                CanEdit: canManage && PurchaseOrder.IsEditable(dto.Status),
                CanDelete: canManage && dto.Status == PurchaseOrderStatus.Draft,
                CanSubmit: canManage && PurchaseOrder.IsEditable(dto.Status),
                CanApprove: canApprove,
                CanCancel: canManage && IsCancellable(dto.Status),
                CanClose: canManage && dto.Status == PurchaseOrderStatus.PartiallyReceived),
        };
    }

    public async Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var sequence = await ledger.NextDocumentNumberAsync(PurchaseOrder.NumberPrefix, request.OrderDate.Year, ct);
        var order = new PurchaseOrder { Number = PurchaseOrder.FormatNumber(request.OrderDate.Year, sequence) };
        await ApplyAsync(order, request, ct);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        await RefreshRequestStatusesAsync(order.Lines.SelectMany(l => l.Sources).Select(s => s.RequestLineId), ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(order.Id, ct);
    }

    public async Task<PurchaseOrderDto> UpdateAsync(Guid id, SavePurchaseOrderRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var order = await LockAndLoadAsync(id, ct);
        if (!PurchaseOrder.IsEditable(order.Status))
            throw new ConflictException($"Order {order.Number} is {order.Status} and can no longer be changed.", ErrorCodes.DocumentNotDraft);

        var previousSources = order.Lines.SelectMany(l => l.Sources).Select(s => s.RequestLineId).ToList();
        // Old and new requests in one call, so the locks are taken in a single id order.
        await LockRequestsAsync(previousSources.Concat(request.Lines.SelectMany(l => l.SourceLineIds)), ct);
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        order.Lines.Clear();
        await db.SaveChangesAsync(ct); // old allocations must be gone before remaining quantities are computed

        await ApplyAsync(order, request, ct);
        order.Status = PurchaseOrderStatus.Draft;
        order.ApprovalLevel = null;
        order.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await RefreshRequestStatusesAsync(previousSources.Concat(order.Lines.SelectMany(l => l.Sources).Select(s => s.RequestLineId)), ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var order = await LockAndLoadAsync(id, ct);
        if (order.Status != PurchaseOrderStatus.Draft)
            throw new ConflictException($"Order {order.Number} is {order.Status} and can no longer be deleted.", ErrorCodes.DocumentNotDraft);

        var sources = order.Lines.SelectMany(l => l.Sources).Select(s => s.RequestLineId).ToList();
        await LockRequestsAsync(sources, ct);
        db.PurchaseOrders.Remove(order);
        await db.SaveChangesAsync(ct);
        await RefreshRequestStatusesAsync(sources, ct);
        await tx.CompleteAsync(ct);
    }

    public async Task<PurchaseOrderDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var order = await LockAndLoadAsync(id, ct);
        EnsureStatus(order, PurchaseOrderStatus.Draft, PurchaseOrderStatus.Rejected);
        await ValidateReferencesAsync(order.SupplierId, order.WarehouseId, order.Lines.Select(l => l.MaterialId).ToList(), ct);

        var threshold = await db.PurchasingSettings.Where(s => s.Id == PurchasingSettings.SingletonId)
            .Select(s => (decimal?)s.DirectorApprovalThreshold).SingleOrDefaultAsync(ct)
            ?? PurchasingSettings.DefaultDirectorApprovalThreshold;
        var scope = await scopeProvider.GetAsync(ct);
        order.Status = PurchaseOrderStatus.Submitted;
        order.ApprovalLevel = PurchasingRules.ApprovalLevelFor(order.Total, threshold);
        order.SubmittedAt = timeProvider.GetUtcNow();
        order.SubmittedBy = scope.UserName;
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var order = await LockAndLoadAsync(id, ct);
        EnsureStatus(order, PurchaseOrderStatus.Submitted);
        var scope = await EnsureApproverAsync(order, ct);

        order.Status = PurchaseOrderStatus.Approved;
        order.ApprovedAt = timeProvider.GetUtcNow();
        order.ApprovedBy = scope.UserName;
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseOrderDto> RejectAsync(Guid id, ReasonRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var order = await LockAndLoadAsync(id, ct);
        EnsureStatus(order, PurchaseOrderStatus.Submitted);
        var scope = await EnsureApproverAsync(order, ct);

        order.Status = PurchaseOrderStatus.Rejected;
        order.RejectedAt = timeProvider.GetUtcNow();
        order.RejectedBy = scope.UserName;
        order.RejectReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseOrderDto> CancelAsync(Guid id, ReasonRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var order = await LockAndLoadAsync(id, ct);
        if (!IsCancellable(order.Status))
            throw WrongStatus(order);

        var sources = order.Lines.SelectMany(l => l.Sources).Select(s => s.RequestLineId).ToList();
        await LockRequestsAsync(sources, ct);
        var scope = await scopeProvider.GetAsync(ct);
        order.Status = PurchaseOrderStatus.Cancelled;
        order.CancelledAt = timeProvider.GetUtcNow();
        order.CancelledBy = scope.UserName;
        order.CancelReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await RefreshRequestStatusesAsync(sources, ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseOrderDto> CloseAsync(Guid id, ReasonRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var order = await LockAndLoadAsync(id, ct);
        EnsureStatus(order, PurchaseOrderStatus.PartiallyReceived);

        var scope = await scopeProvider.GetAsync(ct);
        order.Status = PurchaseOrderStatus.Closed;
        order.ClosedAt = timeProvider.GetUtcNow();
        order.ClosedBy = scope.UserName;
        order.CloseReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<LastPriceDto>> LastPricesAsync(
        IReadOnlyList<Guid> materialIds, Guid? supplierId, CancellationToken ct = default)
    {
        if (materialIds.Count == 0) return [];
        var ids = materialIds.Distinct().Take(PurchaseOrder.MaxLines).ToList();

        // Latest line per (material, supplier); then prefer the requested supplier.
        var candidates = await db.PurchaseOrderLines.AsNoTracking()
            .Where(l => ids.Contains(l.MaterialId) && PricedStatuses.Contains(l.Order.Status))
            .GroupBy(l => new { l.MaterialId, l.Order.SupplierId })
            .Select(g => g.OrderByDescending(l => l.Order.OrderDate).ThenByDescending(l => l.Order.ApprovedAt)
                .Select(l => new
                {
                    l.MaterialId,
                    l.UnitPrice,
                    l.Order.SupplierId,
                    SupplierName = l.Order.Supplier.Name,
                    l.Order.Number,
                    l.Order.OrderDate,
                    l.Order.ApprovedAt,
                })
                .First())
            .ToListAsync(ct);

        return candidates
            .GroupBy(c => c.MaterialId)
            .Select(g => g.OrderByDescending(c => c.SupplierId == supplierId)
                .ThenByDescending(c => c.OrderDate).ThenByDescending(c => c.ApprovedAt)
                .First())
            .Select(c => new LastPriceDto(c.MaterialId, c.UnitPrice, c.SupplierId, c.SupplierName, c.SupplierId == supplierId,
                c.Number, c.OrderDate))
            .ToList();
    }

    public async Task<PagedResult<PriceHistoryDto>> PriceHistoryAsync(PriceHistoryQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, PriceHistoryQuery.MaxPageSize);

        var lines = db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.MaterialId == query.MaterialId && PricedStatuses.Contains(l.Order.Status));
        if (query.SupplierId is { } supplierId) lines = lines.Where(l => l.Order.SupplierId == supplierId);

        var total = await lines.CountAsync(ct);
        var items = await lines
            .OrderByDescending(l => l.Order.OrderDate).ThenByDescending(l => l.Order.Number)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new PriceHistoryDto(l.OrderId, l.Order.Number, l.Order.OrderDate, l.Order.Status,
                l.Order.SupplierId, l.Order.Supplier.Name, l.Quantity, l.UnitPrice, l.Order.VatRate))
            .ToListAsync(ct);
        return new PagedResult<PriceHistoryDto>(items, page, pageSize, total);
    }

    private async Task<PurchaseOrder> LockAndLoadAsync(Guid id, CancellationToken ct)
    {
        await locker.LockAsync<PurchaseOrder>([id], ct);
        return await db.PurchaseOrders.Include(o => o.Lines).ThenInclude(l => l.Sources).SingleOrDefaultAsync(o => o.Id == id, ct)
               ?? throw NotFoundException.For<PurchaseOrder>(id);
    }

    private async Task LockRequestsAsync(IEnumerable<Guid> requestLineIds, CancellationToken ct)
    {
        var lineIds = requestLineIds.Distinct().ToList();
        if (lineIds.Count == 0) return;
        var requestIds = await db.PurchaseRequestLines.Where(l => lineIds.Contains(l.Id)).Select(l => l.RequestId).Distinct().ToListAsync(ct);
        await locker.LockAsync<PurchaseRequest>(requestIds, ct);
    }

    /// <summary>Header, lines, allocations and totals from the request. Locks the source requests first.</summary>
    private async Task ApplyAsync(PurchaseOrder order, SavePurchaseOrderRequest request, CancellationToken ct)
    {
        await ValidateReferencesAsync(request.SupplierId, request.WarehouseId, request.Lines.Select(l => l.MaterialId).ToList(), ct);

        var sourceIds = request.Lines.SelectMany(l => l.SourceLineIds).Distinct().ToList();
        await LockRequestsAsync(sourceIds, ct);

        var sourceLines = await db.PurchaseRequestLines.AsNoTracking()
            .Where(l => sourceIds.Contains(l.Id))
            .Select(l => new
            {
                l.Id,
                l.MaterialId,
                l.Quantity,
                l.Request.Number,
                l.Request.Status,
                // This order's own allocations were removed before we got here.
                Ordered = db.PurchaseOrderLineSources
                    .Where(s => s.RequestLineId == l.Id && s.OrderLine.Order.Status != PurchaseOrderStatus.Cancelled)
                    .Sum(s => (decimal?)s.Quantity) ?? 0,
            })
            .ToDictionaryAsync(l => l.Id, ct);

        var failures = new List<ValidationFailure>();
        var lines = new List<PurchaseOrderLine>();
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var path = $"Lines[{i}]";
            var sources = new List<(Guid, decimal)>();
            for (var j = 0; j < line.SourceLineIds.Count; j++)
            {
                var sourceId = line.SourceLineIds[j];
                if (!sourceLines.TryGetValue(sourceId, out var source))
                    failures.Add(new ValidationFailure($"{path}.SourceLineIds[{j}]", "Purchase request line does not exist."));
                else if (source.MaterialId != line.MaterialId)
                    failures.Add(new ValidationFailure($"{path}.SourceLineIds[{j}]", $"Request {source.Number} line is for another material."));
                else if (!(PurchaseRequest.IsOrderable(source.Status) || source.Status == PurchaseRequestStatus.Ordered))
                    failures.Add(new ValidationFailure($"{path}.SourceLineIds[{j}]", $"Request {source.Number} is {source.Status} and cannot be ordered."));
                else
                    sources.Add((sourceId, source.Quantity - source.Ordered));
            }
            if (sources.Count != line.SourceLineIds.Count) continue;

            var (allocations, over) = PurchasingRules.Allocate(line.Quantity, sources);
            var reason = TextNormalizer.NullIfBlank(line.OverOrderReason);
            if (over > 0 && reason is null)
                failures.Add(new ValidationFailure($"{path}.OverOrderReason",
                    $"Ordering {over} more than the requests still need; give a reason."));

            lines.Add(new PurchaseOrderLine
            {
                LineNo = i + 1,
                MaterialId = line.MaterialId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Amount = PurchasingRules.LineAmount(line.Quantity, line.UnitPrice),
                Note = TextNormalizer.NullIfBlank(line.Note),
                OverOrderReason = over > 0 ? reason : null,
                Sources = allocations.Select(a => new PurchaseOrderLineSource { RequestLineId = a.RequestLineId, Quantity = a.Quantity }).ToList(),
            });
        }
        if (failures.Count > 0) throw new ValidationException(failures);

        var (subtotal, vat, total) = PurchasingRules.Totals(lines.Select(l => l.Amount), request.VatRate);
        order.SupplierId = request.SupplierId;
        order.OrderDate = request.OrderDate;
        order.ExpectedDate = request.ExpectedDate;
        order.WarehouseId = request.WarehouseId;
        order.PaymentTermDays = request.PaymentTermDays;
        order.VatRate = request.VatRate;
        order.Note = TextNormalizer.NullIfBlank(request.Note);
        order.Subtotal = subtotal;
        order.VatAmount = vat;
        order.Total = total;
        order.Lines = lines;
    }

    private async Task ValidateReferencesAsync(Guid supplierId, Guid warehouseId, IReadOnlyList<Guid> materialIds, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (!await db.Suppliers.AnyAsync(s => s.Id == supplierId && s.IsActive, ct))
            failures.Add(new ValidationFailure(nameof(SavePurchaseOrderRequest.SupplierId), "Supplier does not exist or is inactive."));
        if (!await db.Warehouses.AnyAsync(w => w.Id == warehouseId && w.IsActive, ct))
            failures.Add(new ValidationFailure(nameof(SavePurchaseOrderRequest.WarehouseId), "Warehouse does not exist or is inactive."));
        var active = await db.Materials.Where(m => materialIds.Contains(m.Id) && m.IsActive).Select(m => m.Id).ToListAsync(ct);
        failures.AddRange(materialIds.Select((id, i) => (id, i)).Where(x => !active.Contains(x.id))
            .Select(x => new ValidationFailure($"Lines[{x.i}].MaterialId", "Material does not exist or is inactive.")));
        if (failures.Count > 0) throw new ValidationException(failures);
    }

    /// <summary>Recomputes Approved / PartiallyOrdered / Ordered of the requests behind these lines (already locked).</summary>
    private async Task RefreshRequestStatusesAsync(IEnumerable<Guid> requestLineIds, CancellationToken ct)
    {
        var lineIds = requestLineIds.Distinct().ToList();
        if (lineIds.Count == 0) return;

        var requests = await db.PurchaseRequests
            .Where(r => r.Lines.Any(l => lineIds.Contains(l.Id)))
            .Include(r => r.Lines)
            .ToListAsync(ct);
        var requestIds = requests.Select(r => r.Id).ToList();
        var ordered = await db.PurchaseOrderLineSources
            .Where(s => requestIds.Contains(s.RequestLine.RequestId) && s.OrderLine.Order.Status != PurchaseOrderStatus.Cancelled)
            .GroupBy(s => s.RequestLineId)
            .Select(g => new { g.Key, Quantity = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Quantity, ct);

        foreach (var request in requests)
        {
            request.Status = PurchasingRules.RequestStatusAfterOrdering(request.Status,
                request.Lines.Select(l => (l.Quantity, ordered.GetValueOrDefault(l.Id))).ToList());
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<Guid?> PurchasingDepartmentIdAsync(CancellationToken ct) =>
        await db.PurchasingSettings.Where(s => s.Id == PurchasingSettings.SingletonId)
            .Select(s => s.PurchasingDepartmentId).SingleOrDefaultAsync(ct);

    private async Task<UserScope> EnsureApproverAsync(PurchaseOrder order, CancellationToken ct)
    {
        var scope = await scopeProvider.GetAsync(ct);
        if (order.ApprovalLevel is not { } level
            || !PurchasingRules.CanApproveOrder(scope, level, await PurchasingDepartmentIdAsync(ct), order.CreatedBy, order.SubmittedBy))
        {
            throw new ForbiddenException(
                string.Equals(scope.UserName, order.CreatedBy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(scope.UserName, order.SubmittedBy, StringComparison.OrdinalIgnoreCase)
                    ? "You cannot approve an order you created or submitted."
                    : order.ApprovalLevel == PurchaseOrderApprovalLevel.Director
                        ? "This order is above the approval limit and needs a director."
                        : "Only the head of the purchasing department or a director can approve this order.");
        }
        return scope;
    }

    private static bool IsCancellable(PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Approved
            or PurchaseOrderStatus.Rejected;

    private static void EnsureStatus(PurchaseOrder order, params PurchaseOrderStatus[] allowed)
    {
        if (!allowed.Contains(order.Status)) throw WrongStatus(order);
    }

    private static ConflictException WrongStatus(PurchaseOrder order) =>
        new($"Order {order.Number} is {order.Status}; this action is not allowed.", ErrorCodes.InvalidStatus);
}
