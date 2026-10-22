using FluentValidation.Results;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Domain.Features.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Features.Purchasing.Receiving;

/// <summary>One order line that a receipt would take past its limit (409 OverReceipt details).</summary>
public sealed record OverReceiptLine(int LineNo, string MaterialCode, decimal Ordered, decimal Received, decimal Receiving, decimal Allowed);

/// <summary>
/// The purchasing side of a stock receipt linked to a purchase order. Called by the stock document
/// service inside its transaction, after it has locked the document.
/// </summary>
public interface IPurchaseReceiving
{
    /// <summary>Order is receivable, same warehouse, every line names a line of that order with the same material.</summary>
    Task<IReadOnlyList<ValidationFailure>> ValidateAsync(StockDocumentDraft draft, CancellationToken ct = default);

    Task<string?> SupplierNameAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Locks the order, rejects receiving past ordered + tolerance (409 OverReceipt) and moves the
    /// order to PartiallyReceived / Received. Call before the document is marked approved.
    /// </summary>
    Task ApplyApprovalAsync(StockDocument receipt, CancellationToken ct = default);

    /// <summary>Recomputes the order status after an approved receipt was cancelled (document already saved as Cancelled).</summary>
    Task ApplyCancellationAsync(StockDocument receipt, CancellationToken ct = default);
}

public sealed class PurchaseReceiving(IAppDbContext db, IRowLocker locker) : IPurchaseReceiving
{
    public static bool IsReceivable(PurchaseOrderStatus status) =>
        status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived;

    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(StockDocumentDraft draft, CancellationToken ct = default)
    {
        if (draft.PurchaseOrderId is not { } orderId) return [];

        var order = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.Number, o.Status, o.WarehouseId, Lines = o.Lines.Select(l => new { l.Id, l.MaterialId }).ToList() })
            .SingleOrDefaultAsync(ct);
        if (order is null)
            return [new ValidationFailure(nameof(draft.PurchaseOrderId), "Purchase order does not exist.")];
        if (!IsReceivable(order.Status))
            return [new ValidationFailure(nameof(draft.PurchaseOrderId), $"Order {order.Number} is {order.Status} and cannot be received.")];

        var failures = new List<ValidationFailure>();
        if (draft.WarehouseId != order.WarehouseId)
            failures.Add(new ValidationFailure(nameof(draft.WarehouseId), $"Order {order.Number} is delivered to another warehouse."));
        var orderLines = order.Lines.ToDictionary(l => l.Id, l => l.MaterialId);
        for (var i = 0; i < draft.Lines.Count; i++)
        {
            var line = draft.Lines[i];
            if (line.PurchaseOrderLineId is not { } orderLineId || !orderLines.TryGetValue(orderLineId, out var materialId))
                failures.Add(new ValidationFailure($"Lines[{i}].PurchaseOrderLineId", $"Not a line of order {order.Number}."));
            else if (materialId != line.MaterialId)
                failures.Add(new ValidationFailure($"Lines[{i}].MaterialId", "Material differs from the order line."));
        }
        return failures;
    }

    public async Task<string?> SupplierNameAsync(Guid orderId, CancellationToken ct = default) =>
        await db.PurchaseOrders.Where(o => o.Id == orderId).Select(o => o.Supplier.Name).SingleOrDefaultAsync(ct);

    public async Task ApplyApprovalAsync(StockDocument receipt, CancellationToken ct = default)
    {
        if (receipt.PurchaseOrderId is not { } orderId) return;

        var order = await LockAndLoadAsync(orderId, ct);
        if (!IsReceivable(order.Status))
            throw new FluentValidation.ValidationException([new ValidationFailure(nameof(receipt.PurchaseOrderId),
                $"Order {order.Number} is {order.Status} and cannot be received.")]);

        var received = await ReceivedAsync(orderId, ct); // approved receipts only; this one is still a draft
        var receiving = receipt.Lines.GroupBy(l => l.PurchaseOrderLineId!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var materials = await db.Materials.Where(m => order.Lines.Select(l => l.MaterialId).Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => new { m.Code, m.ReceiptTolerancePercent }, ct);

        var over = new List<OverReceiptLine>();
        foreach (var line in order.Lines.OrderBy(l => l.LineNo))
        {
            if (!receiving.TryGetValue(line.Id, out var now)) continue;
            var material = materials[line.MaterialId];
            var before = received.GetValueOrDefault(line.Id);
            var allowed = PurchasingRules.MaxReceivable(line.Quantity, material.ReceiptTolerancePercent);
            if (before + now > allowed)
                over.Add(new OverReceiptLine(line.LineNo, material.Code, line.Quantity, before, now, allowed));
        }
        if (over.Count > 0)
            throw new ConflictException(
                $"Receiving more than order {order.Number} allows: {string.Join("; ", over.Select(o =>
                    $"line {o.LineNo} {o.MaterialCode} ordered {o.Ordered}, received {o.Received}, receiving {o.Receiving}, allowed {o.Allowed}"))}.",
                ErrorCodes.OverReceipt, over);

        order.Status = PurchasingRules.OrderStatusAfterReceiving(order.Status,
            order.Lines.Select(l => (l.Quantity, received.GetValueOrDefault(l.Id) + receiving.GetValueOrDefault(l.Id))).ToList());
    }

    public async Task ApplyCancellationAsync(StockDocument receipt, CancellationToken ct = default)
    {
        if (receipt.PurchaseOrderId is not { } orderId) return;

        var order = await LockAndLoadAsync(orderId, ct);
        var received = await ReceivedAsync(orderId, ct);
        order.Status = PurchasingRules.OrderStatusAfterReceiving(order.Status,
            order.Lines.Select(l => (l.Quantity, received.GetValueOrDefault(l.Id))).ToList());
    }

    private async Task<PurchaseOrder> LockAndLoadAsync(Guid orderId, CancellationToken ct)
    {
        await locker.LockAsync<PurchaseOrder>([orderId], ct);
        return await db.PurchaseOrders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId, ct);
    }

    /// <summary>Quantity on approved receipts per order line.</summary>
    private Task<Dictionary<Guid, decimal>> ReceivedAsync(Guid orderId, CancellationToken ct) =>
        db.StockDocumentLines
            .Where(l => l.PurchaseOrderLine != null && l.PurchaseOrderLine.OrderId == orderId
                        && l.Document.Status == StockDocumentStatus.Approved)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .Select(g => new { g.Key, Quantity = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Quantity, ct);
}
