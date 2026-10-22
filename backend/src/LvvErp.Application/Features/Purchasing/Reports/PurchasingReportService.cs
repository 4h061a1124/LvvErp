using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Reports;

public sealed record StatusCountDto<TStatus>(TStatus Status, int Count, decimal Total);

public sealed record PurchasingSummaryDto(
    IReadOnlyList<StatusCountDto<PurchaseRequestStatus>> Requests,
    IReadOnlyList<StatusCountDto<PurchaseOrderStatus>> Orders,
    int OverdueOrders,
    decimal OpenOrderValue); // total (VAT included) of approved orders still being delivered

/// <summary>Request/order dates within the period; both ends optional.</summary>
public sealed class PurchasingSummaryQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed record OverdueLineDto(
    Guid OrderId,
    string OrderNumber,
    string SupplierName,
    DateOnly ExpectedDate,
    int DaysLate,
    int LineNo,
    string MaterialCode,
    string MaterialName,
    string UnitName,
    decimal Ordered,
    decimal Received,
    decimal Remaining);

public sealed class OverdueLinesQuery
{
    public const int MaxPageSize = 100;

    public Guid? SupplierId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public interface IPurchasingReportService
{
    Task<PurchasingSummaryDto> SummaryAsync(PurchasingSummaryQuery query, CancellationToken ct = default);
    /// <summary>Lines of overdue orders not yet fully received, most late first.</summary>
    Task<PagedResult<OverdueLineDto>> OverdueLinesAsync(OverdueLinesQuery query, CancellationToken ct = default);
}

public sealed class PurchasingReportService(IAppDbContext db, BusinessClock clock) : IPurchasingReportService
{
    public async Task<PurchasingSummaryDto> SummaryAsync(PurchasingSummaryQuery query, CancellationToken ct = default)
    {
        var requests = db.PurchaseRequests.AsNoTracking().IgnoreQueryFilters();
        if (query.From is { } rf) requests = requests.Where(r => r.RequestDate >= rf);
        if (query.To is { } rt) requests = requests.Where(r => r.RequestDate <= rt);
        var requestCounts = await requests.GroupBy(r => r.Status)
            .Select(g => new StatusCountDto<PurchaseRequestStatus>(g.Key, g.Count(), 0))
            .ToListAsync(ct);

        var orders = db.PurchaseOrders.AsNoTracking();
        if (query.From is { } of) orders = orders.Where(o => o.OrderDate >= of);
        if (query.To is { } ot) orders = orders.Where(o => o.OrderDate <= ot);
        var orderCounts = await orders.GroupBy(o => o.Status)
            .Select(g => new StatusCountDto<PurchaseOrderStatus>(g.Key, g.Count(), g.Sum(o => o.Total)))
            .ToListAsync(ct);

        // Overdue and open value are "now" figures, not limited to the period.
        var today = clock.Today;
        var overdue = await db.PurchaseOrders.CountAsync(PurchaseOrderService.Overdue(today), ct);
        var openValue = await db.PurchaseOrders
            .Where(o => o.Status == PurchaseOrderStatus.Approved || o.Status == PurchaseOrderStatus.PartiallyReceived)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0;

        return new PurchasingSummaryDto(
            requestCounts.OrderBy(c => c.Status).ToList(), orderCounts.OrderBy(c => c.Status).ToList(), overdue, openValue);
    }

    public async Task<PagedResult<OverdueLineDto>> OverdueLinesAsync(OverdueLinesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, OverdueLinesQuery.MaxPageSize);
        var today = clock.Today;

        var lines = db.PurchaseOrderLines.AsNoTracking()
            .Where(l => l.Order.ExpectedDate != null && l.Order.ExpectedDate < today
                        && (l.Order.Status == PurchaseOrderStatus.Approved || l.Order.Status == PurchaseOrderStatus.PartiallyReceived))
            .Select(l => new
            {
                Line = l,
                Received = db.StockDocumentLines
                    .Where(r => r.PurchaseOrderLineId == l.Id && r.Document.Status == StockDocumentStatus.Approved)
                    .Sum(r => (decimal?)r.Quantity) ?? 0,
            })
            .Where(x => x.Received < x.Line.Quantity);
        if (query.SupplierId is { } supplierId) lines = lines.Where(x => x.Line.Order.SupplierId == supplierId);

        var total = await lines.CountAsync(ct);
        var rows = await lines
            .OrderBy(x => x.Line.Order.ExpectedDate).ThenBy(x => x.Line.Order.Number).ThenBy(x => x.Line.LineNo)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Line.OrderId, x.Line.Order.Number, SupplierName = x.Line.Order.Supplier.Name, ExpectedDate = x.Line.Order.ExpectedDate!.Value,
                x.Line.LineNo, x.Line.Material.Code, x.Line.Material.Name, UnitName = x.Line.Material.Unit.Name,
                x.Line.Quantity, x.Received,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new OverdueLineDto(
            r.OrderId, r.Number, r.SupplierName, r.ExpectedDate, today.DayNumber - r.ExpectedDate.DayNumber, r.LineNo,
            r.Code, r.Name, r.UnitName, r.Quantity, r.Received, r.Quantity - r.Received)).ToList();
        return new PagedResult<OverdueLineDto>(items, page, pageSize, total);
    }
}
