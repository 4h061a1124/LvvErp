using LvvErp.Application.Common.Security;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Purchasing;

namespace LvvErp.Application.Features.Purchasing;

/// <summary>Pure purchasing rules: money, allocation of order lines to requests, statuses, approvers.</summary>
public static class PurchasingRules
{
    public const int QuantityDecimals = 3;
    public const int UnitPriceDecimals = 2;
    public const decimal MaxUnitPrice = 999_999_999_999m;

    public static decimal VatPercent(VatRate rate) => rate switch
    {
        VatRate.Exempt or VatRate.Zero => 0m,
        VatRate.Five => 5m,
        VatRate.Eight => 8m,
        VatRate.Ten => 10m,
        _ => throw new ArgumentOutOfRangeException(nameof(rate)),
    };

    /// <summary>Money is whole đồng, rounded half away from zero.</summary>
    public static decimal RoundMoney(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal LineAmount(decimal quantity, decimal unitPrice) => RoundMoney(quantity * unitPrice);

    /// <summary>VAT is computed once on the subtotal, as on a VAT invoice.</summary>
    public static (decimal Subtotal, decimal Vat, decimal Total) Totals(IEnumerable<decimal> lineAmounts, VatRate rate)
    {
        var subtotal = lineAmounts.Sum();
        var vat = RoundMoney(subtotal * VatPercent(rate) / 100m);
        return (subtotal, vat, subtotal + vat);
    }

    /// <summary>Orders at or above the threshold (VAT included) need a Director.</summary>
    public static PurchaseOrderApprovalLevel ApprovalLevelFor(decimal total, decimal directorThreshold) =>
        total >= directorThreshold ? PurchaseOrderApprovalLevel.Director : PurchaseOrderApprovalLevel.PurchasingHead;

    public sealed record Allocation(Guid RequestLineId, decimal Quantity);

    /// <summary>
    /// Covers the request lines in the given order, each up to what it still needs. Every source keeps
    /// a row (possibly 0) so the line's origin is recorded; what is left over is over-ordering.
    /// </summary>
    public static (IReadOnlyList<Allocation> Allocations, decimal OverOrdered) Allocate(
        decimal quantity, IReadOnlyList<(Guid RequestLineId, decimal Remaining)> sources)
    {
        var left = quantity;
        var allocations = new List<Allocation>(sources.Count);
        foreach (var (lineId, remaining) in sources)
        {
            var take = Math.Min(left, Math.Max(0, remaining));
            allocations.Add(new Allocation(lineId, take));
            left -= take;
        }
        return (allocations, left);
    }

    /// <summary>
    /// Status of an approved request from how much of each line is on (non-cancelled) orders.
    /// Other statuses are not driven by ordering and are returned unchanged.
    /// </summary>
    public static PurchaseRequestStatus RequestStatusAfterOrdering(
        PurchaseRequestStatus current, IReadOnlyList<(decimal Quantity, decimal Ordered)> lines)
    {
        if (current is not (PurchaseRequestStatus.Approved or PurchaseRequestStatus.PartiallyOrdered or PurchaseRequestStatus.Ordered))
            return current;
        if (lines.Count > 0 && lines.All(l => l.Ordered >= l.Quantity)) return PurchaseRequestStatus.Ordered;
        if (lines.Any(l => l.Ordered > 0)) return PurchaseRequestStatus.PartiallyOrdered;
        return PurchaseRequestStatus.Approved;
    }

    /// <summary>Most that may be received on an order line: ordered plus the material's tolerance, rounded down to 3 decimals.</summary>
    public static decimal MaxReceivable(decimal ordered, decimal tolerancePercent) =>
        decimal.Round(ordered * (1 + tolerancePercent / 100m), QuantityDecimals, MidpointRounding.ToZero);

    /// <summary>
    /// Approved / PartiallyReceived / Received from the received quantity of each line. Closed, cancelled
    /// and not-yet-approved orders are returned unchanged.
    /// </summary>
    public static PurchaseOrderStatus OrderStatusAfterReceiving(
        PurchaseOrderStatus current, IReadOnlyList<(decimal Ordered, decimal Received)> lines)
    {
        if (current is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received))
            return current;
        if (lines.Count > 0 && lines.All(l => l.Received >= l.Ordered)) return PurchaseOrderStatus.Received;
        if (lines.Any(l => l.Received > 0)) return PurchaseOrderStatus.PartiallyReceived;
        return PurchaseOrderStatus.Approved;
    }

    /// <summary>
    /// Head of the request's department, or a Director; never the requester. Admin holds no approval permission.
    /// </summary>
    public static bool CanApproveRequest(UserScope user, Guid departmentId, Guid requesterId)
    {
        if (user.UserId == requesterId || !user.Can(Permissions.PurchasingApproveRequests)) return false;
        return user.HasRole(Roles.Director)
               || (user.HasRole(Roles.DepartmentHead) && user.DepartmentId is { } own && own == departmentId);
    }

    /// <summary>
    /// Director for any level; head of the purchasing department for <see cref="PurchaseOrderApprovalLevel.PurchasingHead"/>.
    /// Never whoever created or submitted the order.
    /// </summary>
    public static bool CanApproveOrder(
        UserScope user, PurchaseOrderApprovalLevel level, Guid? purchasingDepartmentId, string? createdBy, string? submittedBy)
    {
        if (!user.Can(Permissions.PurchasingApproveOrders)) return false;
        if (string.Equals(user.UserName, createdBy, StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.UserName, submittedBy, StringComparison.OrdinalIgnoreCase))
            return false;
        if (user.HasRole(Roles.Director)) return true;
        return level == PurchaseOrderApprovalLevel.PurchasingHead
               && user.HasRole(Roles.DepartmentHead)
               && purchasingDepartmentId is { } purchasing
               && user.DepartmentId == purchasing;
    }
}
