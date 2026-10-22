using FluentValidation;
using FluentValidation.Results;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Persistence;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Common.Text;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Requests;

public interface IPurchaseRequestService
{
    /// <summary>Everything for holders of Purchasing.View; otherwise the user's own department.</summary>
    Task<PagedResult<PurchaseRequestSummaryDto>> ListAsync(PurchaseRequestListQuery query, CancellationToken ct = default);
    Task<PurchaseRequestDto> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>For the current user's department (409 NoDepartment without a linked employee).</summary>
    Task<PurchaseRequestDto> CreateAsync(SavePurchaseRequestRequest request, CancellationToken ct = default);
    /// <summary>Requester only, while Draft or Rejected (goes back to Draft).</summary>
    Task<PurchaseRequestDto> UpdateAsync(Guid id, SavePurchaseRequestRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseRequestDto> SubmitAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseRequestDto> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseRequestDto> RejectAsync(Guid id, ReasonRequest request, CancellationToken ct = default);
    /// <summary>Only while the request is on no order.</summary>
    Task<PurchaseRequestDto> CancelAsync(Guid id, ReasonRequest request, CancellationToken ct = default);
    /// <summary>Stops further ordering of what is left; not while an unapproved order still uses it.</summary>
    Task<PurchaseRequestDto> CloseAsync(Guid id, ReasonRequest request, CancellationToken ct = default);
    Task<PagedResult<OrderableRequestLineDto>> ListOrderableLinesAsync(OrderableLinesQuery query, CancellationToken ct = default);
}

/// <summary>
/// Writes lock the request row before reading its status. Purchase orders lock the requests they draw
/// from, so ordering, cancelling and closing one request run one after another.
/// </summary>
public sealed class PurchaseRequestService(
    IAppDbContext db,
    IStockLedger ledger,
    IRowLocker locker,
    IUserScopeProvider scopeProvider,
    TimeProvider timeProvider,
    IValidator<SavePurchaseRequestRequest> validator,
    IValidator<ReasonRequest> reasonValidator) : IPurchaseRequestService
{
    public async Task<PagedResult<PurchaseRequestSummaryDto>> ListAsync(PurchaseRequestListQuery query, CancellationToken ct = default)
    {
        var scope = await scopeProvider.GetAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, PurchaseRequestListQuery.MaxPageSize);

        var requests = Visible(scope);
        if (query.Mine) requests = requests.Where(r => r.RequesterId == scope.UserId);
        if (query.Status is { } status) requests = requests.Where(r => r.Status == status);
        if (query.DepartmentId is { } departmentId) requests = requests.Where(r => r.DepartmentId == departmentId);
        if (query.From is { } from) requests = requests.Where(r => r.RequestDate >= from);
        if (query.To is { } to) requests = requests.Where(r => r.RequestDate <= to);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            requests = requests.Where(r =>
                r.Number.ToLower().Contains(term)
                || (r.Note != null && AppDbFunctions.Unaccent(r.Note.ToLower()).Contains(AppDbFunctions.Unaccent(term)))
                || AppDbFunctions.Unaccent(r.Requester.FullName.ToLower()).Contains(AppDbFunctions.Unaccent(term)));
        }

        var total = await requests.CountAsync(ct);
        var descending = query.Sort is null || query.Sort.StartsWith('-');
        var ordered = query.Sort?.TrimStart('-').ToLowerInvariant() == "number"
            ? (descending ? requests.OrderByDescending(r => r.Number) : requests.OrderBy(r => r.Number))
            : (descending
                ? requests.OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Number)
                : requests.OrderBy(r => r.RequestDate).ThenBy(r => r.Number));

        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(Summary).ToListAsync(ct);
        return new PagedResult<PurchaseRequestSummaryDto>(items, page, pageSize, total);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<PurchaseRequest, PurchaseRequestSummaryDto>> Summary =
        r => new PurchaseRequestSummaryDto(
            r.Id, r.Number, r.Status, r.RequestDate, r.NeededDate, r.DepartmentId, r.Department.Name, r.Requester.FullName,
            r.Note, r.Lines.Count, r.CreatedAt, r.SubmittedAt);

    public async Task<PurchaseRequestDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var scope = await scopeProvider.GetAsync(ct);
        var dto = await Visible(scope).Where(r => r.Id == id)
            .Select(r => new PurchaseRequestDto(
                r.Id, r.Number, r.Status, r.RequestDate, r.NeededDate, r.DepartmentId, r.Department.Name,
                r.RequesterId, r.Requester.FullName, r.Note, r.CreatedAt, r.SubmittedAt,
                r.ApprovedAt, r.ApprovedBy, r.RejectedAt, r.RejectedBy, r.RejectReason,
                r.ClosedAt, r.ClosedBy, r.CloseReason, r.CancelledAt, r.CancelledBy, r.CancelReason,
                r.Lines.OrderBy(l => l.LineNo).Select(l => new PurchaseRequestLineDto(
                    l.Id, l.LineNo, l.MaterialId, l.Material.Code, l.Material.Name, l.Material.Unit.Name, l.Quantity,
                    db.PurchaseOrderLineSources
                        .Where(s => s.RequestLineId == l.Id && s.OrderLine.Order.Status != PurchaseOrderStatus.Cancelled)
                        .Sum(s => (decimal?)s.Quantity) ?? 0,
                    l.Note)).ToList(),
                db.PurchaseOrderLineSources
                    .Where(s => s.RequestLine.RequestId == r.Id)
                    .Select(s => s.OrderLine.Order)
                    .Distinct()
                    .OrderBy(o => o.Number)
                    .Select(o => new RelatedOrderDto(o.Id, o.Number, o.Status))
                    .ToList(),
                new PurchaseRequestActions(false, false, false, false, false, false)))
            .SingleOrDefaultAsync(ct)
            ?? throw NotFoundException.For<PurchaseRequest>(id);

        var hasOrders = dto.Orders.Any(o => o.Status != PurchaseOrderStatus.Cancelled);
        var hasOpenOrders = dto.Orders.Any(o => IsOpenOrder(o.Status));
        var isRequester = dto.RequesterId == scope.UserId;
        var canManage = isRequester || scope.Can(Permissions.PurchasingManageOrders);
        return dto with
        {
            Actions = new PurchaseRequestActions(
                CanEdit: isRequester && IsEditable(dto.Status),
                CanDelete: isRequester && dto.Status == PurchaseRequestStatus.Draft,
                CanSubmit: isRequester && IsEditable(dto.Status),
                CanApprove: dto.Status == PurchaseRequestStatus.Submitted
                            && PurchasingRules.CanApproveRequest(scope, dto.DepartmentId, dto.RequesterId),
                CanCancel: canManage && IsCancellable(dto.Status) && !hasOrders,
                CanClose: canManage && PurchaseRequest.IsOrderable(dto.Status) && !hasOpenOrders),
        };
    }

    public async Task<PurchaseRequestDto> CreateAsync(SavePurchaseRequestRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var scope = await scopeProvider.GetAsync(ct);
        if (scope.DepartmentId is not { } departmentId)
            throw new ConflictException("Your account is not linked to an employee, so it has no department.", ErrorCodes.NoDepartment);
        await ValidateMaterialsAsync(request.Lines.Select(l => l.MaterialId).ToList(), ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var sequence = await ledger.NextDocumentNumberAsync(PurchaseRequest.NumberPrefix, request.RequestDate.Year, ct);
        var purchaseRequest = new PurchaseRequest
        {
            Number = PurchaseRequest.FormatNumber(request.RequestDate.Year, sequence),
            DepartmentId = departmentId,
            RequesterId = scope.UserId,
        };
        Apply(purchaseRequest, request);
        db.PurchaseRequests.Add(purchaseRequest);
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(purchaseRequest.Id, ct);
    }

    public async Task<PurchaseRequestDto> UpdateAsync(Guid id, SavePurchaseRequestRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        await ValidateMaterialsAsync(request.Lines.Select(l => l.MaterialId).ToList(), ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var (purchaseRequest, scope) = await LockAndLoadAsync(id, ct);
        EnsureRequester(purchaseRequest, scope);
        if (!IsEditable(purchaseRequest.Status))
            throw NotEditable(purchaseRequest);

        db.PurchaseRequestLines.RemoveRange(purchaseRequest.Lines);
        purchaseRequest.Lines.Clear();
        Apply(purchaseRequest, request);
        purchaseRequest.Status = PurchaseRequestStatus.Draft;
        purchaseRequest.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var (purchaseRequest, scope) = await LockAndLoadAsync(id, ct);
        EnsureRequester(purchaseRequest, scope);
        if (purchaseRequest.Status != PurchaseRequestStatus.Draft)
            throw NotEditable(purchaseRequest);

        db.PurchaseRequests.Remove(purchaseRequest);
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);
    }

    public async Task<PurchaseRequestDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var (purchaseRequest, scope) = await LockAndLoadAsync(id, ct);
        EnsureRequester(purchaseRequest, scope);
        EnsureStatus(purchaseRequest, PurchaseRequestStatus.Draft, PurchaseRequestStatus.Rejected);
        await ValidateMaterialsAsync(purchaseRequest.Lines.Select(l => l.MaterialId).ToList(), ct);

        purchaseRequest.Status = PurchaseRequestStatus.Submitted;
        purchaseRequest.SubmittedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseRequestDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var (purchaseRequest, scope) = await LockAndLoadAsync(id, ct);
        EnsureStatus(purchaseRequest, PurchaseRequestStatus.Submitted);
        EnsureApprover(purchaseRequest, scope);

        purchaseRequest.Status = PurchaseRequestStatus.Approved;
        purchaseRequest.ApprovedAt = timeProvider.GetUtcNow();
        purchaseRequest.ApprovedBy = scope.UserName;
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseRequestDto> RejectAsync(Guid id, ReasonRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var (purchaseRequest, scope) = await LockAndLoadAsync(id, ct);
        EnsureStatus(purchaseRequest, PurchaseRequestStatus.Submitted);
        EnsureApprover(purchaseRequest, scope);

        purchaseRequest.Status = PurchaseRequestStatus.Rejected;
        purchaseRequest.RejectedAt = timeProvider.GetUtcNow();
        purchaseRequest.RejectedBy = scope.UserName;
        purchaseRequest.RejectReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseRequestDto> CancelAsync(Guid id, ReasonRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var (purchaseRequest, scope) = await LockAndLoadAsync(id, ct);
        EnsureCanManage(purchaseRequest, scope);
        if (!IsCancellable(purchaseRequest.Status))
            throw WrongStatus(purchaseRequest);
        if (await db.PurchaseOrderLineSources.AnyAsync(s => s.RequestLine.RequestId == id
                                                            && s.OrderLine.Order.Status != PurchaseOrderStatus.Cancelled, ct))
            throw new ConflictException($"Request {purchaseRequest.Number} is on a purchase order; cancel the order first.",
                ErrorCodes.RequestHasOrders);

        purchaseRequest.Status = PurchaseRequestStatus.Cancelled;
        purchaseRequest.CancelledAt = timeProvider.GetUtcNow();
        purchaseRequest.CancelledBy = scope.UserName;
        purchaseRequest.CancelReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PurchaseRequestDto> CloseAsync(Guid id, ReasonRequest request, CancellationToken ct = default)
    {
        await reasonValidator.ValidateAndThrowAsync(request, ct);

        await using var tx = await DbTransactionScope.BeginAsync(db, ct);
        var (purchaseRequest, scope) = await LockAndLoadAsync(id, ct);
        EnsureCanManage(purchaseRequest, scope);
        if (!PurchaseRequest.IsOrderable(purchaseRequest.Status))
            throw WrongStatus(purchaseRequest);
        var openOrders = await db.PurchaseOrderLineSources
            .Where(s => s.RequestLine.RequestId == id)
            .Select(s => s.OrderLine.Order)
            .Where(o => o.Status == PurchaseOrderStatus.Draft || o.Status == PurchaseOrderStatus.Submitted
                        || o.Status == PurchaseOrderStatus.Rejected)
            .Select(o => o.Number).Distinct().ToListAsync(ct);
        if (openOrders.Count > 0)
            throw new ConflictException(
                $"Order(s) {string.Join(", ", openOrders)} still use this request; approve or cancel them first.",
                ErrorCodes.RequestHasOpenOrders, openOrders);

        purchaseRequest.Status = PurchaseRequestStatus.Closed;
        purchaseRequest.ClosedAt = timeProvider.GetUtcNow();
        purchaseRequest.ClosedBy = scope.UserName;
        purchaseRequest.CloseReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        await tx.CompleteAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PagedResult<OrderableRequestLineDto>> ListOrderableLinesAsync(OrderableLinesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, OrderableLinesQuery.MaxPageSize);

        var lines = db.PurchaseRequestLines.AsNoTracking()
            .Where(l => l.Request.Status == PurchaseRequestStatus.Approved || l.Request.Status == PurchaseRequestStatus.PartiallyOrdered)
            .Select(l => new
            {
                Line = l,
                Ordered = db.PurchaseOrderLineSources
                    .Where(s => s.RequestLineId == l.Id && s.OrderLine.Order.Status != PurchaseOrderStatus.Cancelled)
                    .Sum(s => (decimal?)s.Quantity) ?? 0,
            })
            .Where(x => x.Ordered < x.Line.Quantity);

        if (query.MaterialId is { } materialId) lines = lines.Where(x => x.Line.MaterialId == materialId);
        if (query.DepartmentId is { } departmentId) lines = lines.Where(x => x.Line.Request.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            lines = lines.Where(x =>
                x.Line.Request.Number.ToLower().Contains(term)
                || x.Line.Material.Code.ToLower().Contains(term)
                || AppDbFunctions.Unaccent(x.Line.Material.Name.ToLower()).Contains(AppDbFunctions.Unaccent(term)));
        }

        var total = await lines.CountAsync(ct);
        var items = await lines
            .OrderBy(x => x.Line.Request.NeededDate == null)
            .ThenBy(x => x.Line.Request.NeededDate)
            .ThenBy(x => x.Line.Request.Number)
            .ThenBy(x => x.Line.LineNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OrderableRequestLineDto(
                x.Line.Id, x.Line.RequestId, x.Line.Request.Number, x.Line.Request.Department.Name, x.Line.Request.Requester.FullName,
                x.Line.Request.NeededDate, x.Line.LineNo, x.Line.MaterialId, x.Line.Material.Code, x.Line.Material.Name,
                x.Line.Material.Unit.Name, x.Line.Quantity, x.Ordered, x.Line.Quantity - x.Ordered, x.Line.Note))
            .ToListAsync(ct);
        return new PagedResult<OrderableRequestLineDto>(items, page, pageSize, total);
    }

    private IQueryable<PurchaseRequest> Visible(UserScope scope)
    {
        // IgnoreQueryFilters: keep showing requests of a department that was deleted later.
        var requests = db.PurchaseRequests.AsNoTracking().IgnoreQueryFilters();
        return scope.Can(Permissions.PurchasingView)
            ? requests
            : requests.Where(r => r.RequesterId == scope.UserId || (scope.DepartmentId != null && r.DepartmentId == scope.DepartmentId));
    }

    private async Task<(PurchaseRequest Request, UserScope Scope)> LockAndLoadAsync(Guid id, CancellationToken ct)
    {
        var scope = await scopeProvider.GetAsync(ct);
        await locker.LockAsync<PurchaseRequest>([id], ct);
        var purchaseRequest = await db.PurchaseRequests.IgnoreQueryFilters().Include(r => r.Lines).SingleOrDefaultAsync(r => r.Id == id, ct);
        // Requests the user can't see are reported as missing, like GET does.
        if (purchaseRequest is null
            || !(scope.Can(Permissions.PurchasingView) || purchaseRequest.RequesterId == scope.UserId
                 || purchaseRequest.DepartmentId == scope.DepartmentId))
            throw NotFoundException.For<PurchaseRequest>(id);
        return (purchaseRequest, scope);
    }

    private static bool IsEditable(PurchaseRequestStatus status) =>
        status is PurchaseRequestStatus.Draft or PurchaseRequestStatus.Rejected;

    private static bool IsCancellable(PurchaseRequestStatus status) =>
        status is PurchaseRequestStatus.Draft or PurchaseRequestStatus.Submitted or PurchaseRequestStatus.Approved
            or PurchaseRequestStatus.Rejected;

    /// <summary>Orders that may still change what they take from the request.</summary>
    private static bool IsOpenOrder(PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted or PurchaseOrderStatus.Rejected;

    private static void EnsureRequester(PurchaseRequest request, UserScope scope)
    {
        if (request.RequesterId != scope.UserId)
            throw new ForbiddenException("Only the requester can change this request.");
    }

    private static void EnsureCanManage(PurchaseRequest request, UserScope scope)
    {
        if (request.RequesterId != scope.UserId && !scope.Can(Permissions.PurchasingManageOrders))
            throw new ForbiddenException();
    }

    private static void EnsureApprover(PurchaseRequest request, UserScope scope)
    {
        if (!PurchasingRules.CanApproveRequest(scope, request.DepartmentId, request.RequesterId))
            throw new ForbiddenException(request.RequesterId == scope.UserId
                ? "You cannot approve your own request."
                : "Only the head of the requesting department or a director can approve this request.");
    }

    private static void EnsureStatus(PurchaseRequest request, params PurchaseRequestStatus[] allowed)
    {
        if (!allowed.Contains(request.Status)) throw WrongStatus(request);
    }

    private static ConflictException WrongStatus(PurchaseRequest request) =>
        new($"Request {request.Number} is {request.Status}; this action is not allowed.", ErrorCodes.InvalidStatus);

    private static ConflictException NotEditable(PurchaseRequest request) =>
        new($"Request {request.Number} is {request.Status} and can no longer be changed.", ErrorCodes.DocumentNotDraft);

    private static void Apply(PurchaseRequest purchaseRequest, SavePurchaseRequestRequest request)
    {
        purchaseRequest.RequestDate = request.RequestDate;
        purchaseRequest.NeededDate = request.NeededDate;
        purchaseRequest.Note = TextNormalizer.NullIfBlank(request.Note);
        purchaseRequest.Lines = request.Lines.Select((l, i) => new PurchaseRequestLine
        {
            LineNo = i + 1,
            MaterialId = l.MaterialId,
            Quantity = l.Quantity,
            Note = TextNormalizer.NullIfBlank(l.Note),
        }).ToList();
    }

    private async Task ValidateMaterialsAsync(IReadOnlyList<Guid> materialIds, CancellationToken ct)
    {
        var active = await db.Materials.Where(m => materialIds.Contains(m.Id) && m.IsActive).Select(m => m.Id).ToListAsync(ct);
        var failures = materialIds
            .Select((id, i) => (id, i))
            .Where(x => !active.Contains(x.id))
            .Select(x => new ValidationFailure($"Lines[{x.i}].MaterialId", "Material does not exist or is inactive."))
            .ToList();
        if (failures.Count > 0) throw new ValidationException(failures);
    }
}
