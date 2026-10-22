using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Approvals;

/// <summary>Submitted documents the current user can approve ("Chờ tôi duyệt").</summary>
public sealed record ApprovalInboxDto(
    IReadOnlyList<PurchaseRequestSummaryDto> Requests,
    IReadOnlyList<PurchaseOrderSummaryDto> Orders);

public interface IApprovalInboxService
{
    Task<ApprovalInboxDto> GetAsync(CancellationToken ct = default);
}

/// <summary>Same rules as <see cref="PurchasingRules.CanApproveRequest"/> and <see cref="PurchasingRules.CanApproveOrder"/>, as queries.</summary>
public sealed class ApprovalInboxService(IAppDbContext db, IUserScopeProvider scopeProvider, BusinessClock clock) : IApprovalInboxService
{
    private const int MaxItems = 200;

    public async Task<ApprovalInboxDto> GetAsync(CancellationToken ct = default)
    {
        var scope = await scopeProvider.GetAsync(ct);
        var isDirector = scope.HasRole(Roles.Director);
        var isHead = scope.HasRole(Roles.DepartmentHead) && scope.DepartmentId is not null;

        IReadOnlyList<PurchaseRequestSummaryDto> requests = [];
        if (scope.Can(Permissions.PurchasingApproveRequests) && (isDirector || isHead))
        {
            requests = await db.PurchaseRequests.AsNoTracking().IgnoreQueryFilters()
                .Where(r => r.Status == PurchaseRequestStatus.Submitted && r.RequesterId != scope.UserId
                            && (isDirector || r.DepartmentId == scope.DepartmentId))
                .OrderBy(r => r.SubmittedAt)
                .Take(MaxItems)
                .Select(PurchaseRequestService.Summary)
                .ToListAsync(ct);
        }

        IReadOnlyList<PurchaseOrderSummaryDto> orders = [];
        if (scope.Can(Permissions.PurchasingApproveOrders))
        {
            var purchasingDepartmentId = await db.PurchasingSettings.Where(s => s.Id == PurchasingSettings.SingletonId)
                .Select(s => s.PurchasingDepartmentId).SingleOrDefaultAsync(ct);
            var isPurchasingHead = isHead && purchasingDepartmentId is not null && scope.DepartmentId == purchasingDepartmentId;
            if (isDirector || isPurchasingHead)
            {
                var userName = scope.UserName;
                orders = await db.PurchaseOrders.AsNoTracking()
                    .Where(o => o.Status == PurchaseOrderStatus.Submitted
                                && o.CreatedBy != userName && o.SubmittedBy != userName
                                && (isDirector || o.ApprovalLevel == PurchaseOrderApprovalLevel.PurchasingHead))
                    .OrderBy(o => o.SubmittedAt)
                    .Take(MaxItems)
                    .Select(PurchaseOrderService.Summary(clock.Today))
                    .ToListAsync(ct);
            }
        }

        return new ApprovalInboxDto(requests, orders);
    }
}
