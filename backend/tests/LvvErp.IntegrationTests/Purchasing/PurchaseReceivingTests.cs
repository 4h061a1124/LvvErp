using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Application.Features.Purchasing.Reports;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Domain.Features.Purchasing;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Purchasing;

public sealed class PurchaseReceivingTests(ApiFactory factory) : PurchasingTestBase(factory)
{
    private sealed record Setup(Actor Purchaser, Actor Storekeeper, MaterialDto Material, Guid WarehouseId, PurchaseOrderDto Order);

    /// <summary>An approved order of <paramref name="quantity"/> of one new material.</summary>
    private async Task<Setup> ApprovedOrderAsync(
        decimal quantity = 10, decimal tolerancePercent = 0, DateOnly? orderDate = null, DateOnly? expectedDate = null)
    {
        var admin = await AdminClientAsync();
        var purchaser = await PurchaserAsync();
        var head = await PurchasingHeadAsync();
        var storekeeper = await UserInAsync("KHO", Roles.Storekeeper);
        var supplier = await CreateSupplierAsync(purchaser.Client);
        var site = await CreateWarehouseAsync(admin);
        var material = await CreateMaterialAsync(admin);
        if (tolerancePercent > 0)
        {
            material = await ReadAsync<MaterialDto>(await admin.PutAsJsonAsync($"/api/materials/{material.Id}", new SaveMaterialRequest(
                material.Code, material.Name, null, material.UnitId, material.GroupId, 0, false, ReceiptTolerancePercent: tolerancePercent), Json));
        }
        var request = await ApprovedRequestAsync((material.Id, quantity));

        var body = NewOrder(supplier.Id, site.Warehouse.Id, OrderLine(request.Lines.Single()));
        if (orderDate is not null) body = body with { OrderDate = orderDate.Value, ExpectedDate = expectedDate };
        var order = await PostOrderAsync(purchaser.Client, "/api/purchase-orders", body);
        await PostOrderAsync(purchaser.Client, $"/api/purchase-orders/{order.Id}/submit");
        order = await PostOrderAsync(head.Client, $"/api/purchase-orders/{order.Id}/approve");
        return new Setup(purchaser, storekeeper, material, site.Warehouse.Id, order);
    }

    private static SaveStockReceiptRequest ReceiptFor(Setup s, params decimal[] quantities) =>
        new(Today, s.WarehouseId, StockDocumentReason.Purchase, null, null,
            quantities.Select(q => new ReceiptLineRequest(s.Material.Id, null, null, null, null, q, null, s.Order.Lines.Single().Id)).ToList(),
            s.Order.Id);

    [Fact]
    public async Task Order_is_received_in_parts()
    {
        var s = await ApprovedOrderAsync(10);

        var first = await CreateDraftAsync(s.Storekeeper.Client, "stock-receipts", ReceiptFor(s, 4));
        Assert.Equal((s.Order.Id, s.Order.Number, s.Order.Supplier.Name), (first.PurchaseOrderId, first.PurchaseOrderNumber, first.Partner));
        Assert.Equal(PurchaseOrderStatus.Approved, (await GetOrderAsync(s.Purchaser.Client, s.Order.Id)).Status); // drafts don't count

        await ApproveAsync(s.Storekeeper.Client, "stock-receipts", first.Id);
        var partly = await GetOrderAsync(s.Purchaser.Client, s.Order.Id);
        Assert.Equal((PurchaseOrderStatus.PartiallyReceived, 4m), (partly.Status, partly.Lines.Single().ReceivedQuantity));

        // Two receipt lines on one order line (e.g. two lots) add up.
        var second = await CreateDraftAsync(s.Storekeeper.Client, "stock-receipts", ReceiptFor(s, 2, 4));
        await ApproveAsync(s.Storekeeper.Client, "stock-receipts", second.Id);
        var done = await GetOrderAsync(s.Purchaser.Client, s.Order.Id);
        Assert.Equal((PurchaseOrderStatus.Received, 10m), (done.Status, done.Lines.Single().ReceivedQuantity));
        Assert.Equal([first.Number, second.Number], done.Receipts.Select(r => r.Number));
        Assert.Equal(10m, await QuantityAsync(s.Storekeeper.Client, s.Material.Id, s.WarehouseId));

        // Cancelling a receipt (Admin) reopens the order.
        var admin = await AdminClientAsync();
        await PostAsync(admin, $"/api/stock-receipts/{second.Id}/cancel", new CancelStockDocumentRequest("Nhập nhầm"));
        var reopened = await GetOrderAsync(s.Purchaser.Client, s.Order.Id);
        Assert.Equal((PurchaseOrderStatus.PartiallyReceived, 4m), (reopened.Status, reopened.Lines.Single().ReceivedQuantity));
    }

    [Fact]
    public async Task Receiving_more_than_ordered_is_rejected_unless_within_tolerance()
    {
        var strict = await ApprovedOrderAsync(10);
        var draft = await CreateDraftAsync(strict.Storekeeper.Client, "stock-receipts", ReceiptFor(strict, 10.001m));

        var response = await strict.Storekeeper.Client.PostAsync($"/api/stock-receipts/{draft.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.OverReceipt, await ErrorCodeAsync(response));
        var detail = (await ProblemAsync(response)).GetProperty("details")[0];
        Assert.Equal((10m, 0m, 10.001m, 10m), (detail.GetProperty("ordered").GetDecimal(), detail.GetProperty("received").GetDecimal(),
            detail.GetProperty("receiving").GetDecimal(), detail.GetProperty("allowed").GetDecimal()));
        Assert.Equal(0m, await QuantityAsync(strict.Storekeeper.Client, strict.Material.Id, strict.WarehouseId)); // nothing posted

        var loose = await ApprovedOrderAsync(10, tolerancePercent: 5);
        var within = await CreateDraftAsync(loose.Storekeeper.Client, "stock-receipts", ReceiptFor(loose, 10.5m));
        await ApproveAsync(loose.Storekeeper.Client, "stock-receipts", within.Id);
        Assert.Equal(PurchaseOrderStatus.Received, (await GetOrderAsync(loose.Purchaser.Client, loose.Order.Id)).Status);
    }

    [Fact]
    public async Task Receipt_must_match_the_order()
    {
        var s = await ApprovedOrderAsync(10);
        var other = await ApprovedOrderAsync(10);

        var wrongWarehouse = await PostAsync(s.Storekeeper.Client, "/api/stock-receipts", ReceiptFor(s, 1) with { WarehouseId = other.WarehouseId });
        Assert.Equal(HttpStatusCode.BadRequest, wrongWarehouse.StatusCode);
        Assert.True((await ProblemAsync(wrongWarehouse)).GetProperty("errors").TryGetProperty("WarehouseId", out _));

        var foreignLine = ReceiptFor(s, 1) with
        {
            Lines = [new ReceiptLineRequest(other.Material.Id, null, null, null, null, 1, null, other.Order.Lines.Single().Id)],
        };
        var response = await PostAsync(s.Storekeeper.Client, "/api/stock-receipts", foreignLine);
        Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty("Lines[0].PurchaseOrderLineId", out _));

        var unlinked = ReceiptFor(s, 1) with { Reason = StockDocumentReason.Other };
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(s.Storekeeper.Client, "/api/stock-receipts", unlinked)).StatusCode);
    }

    [Fact]
    public async Task Partly_received_order_cannot_be_cancelled_but_can_be_closed_and_then_takes_no_more_goods()
    {
        var s = await ApprovedOrderAsync(10);
        var first = await CreateDraftAsync(s.Storekeeper.Client, "stock-receipts", ReceiptFor(s, 3));
        await ApproveAsync(s.Storekeeper.Client, "stock-receipts", first.Id);
        var pending = await CreateDraftAsync(s.Storekeeper.Client, "stock-receipts", ReceiptFor(s, 2));

        var cancel = await s.Purchaser.Client.PostAsJsonAsync($"/api/purchase-orders/{s.Order.Id}/cancel", Reason(), Json);
        Assert.Equal(ErrorCodes.InvalidStatus, await ErrorCodeAsync(cancel));
        Assert.True((await GetOrderAsync(s.Purchaser.Client, s.Order.Id)).Actions.CanClose);

        var closed = await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{s.Order.Id}/close", Reason("NCC ngừng cung cấp"));
        Assert.Equal(PurchaseOrderStatus.Closed, closed.Status);

        var approvePending = await s.Storekeeper.Client.PostAsync($"/api/stock-receipts/{pending.Id}/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, approvePending.StatusCode);
    }

    [Fact]
    public async Task Concurrent_receipts_cannot_together_exceed_the_order()
    {
        var s = await ApprovedOrderAsync(10);
        var otherStorekeeper = await UserInAsync("KHO", Roles.Storekeeper);

        for (var round = 0; round < 3; round++)
        {
            var a = await CreateDraftAsync(s.Storekeeper.Client, "stock-receipts", ReceiptFor(s, 6));
            var b = await CreateDraftAsync(s.Storekeeper.Client, "stock-receipts", ReceiptFor(s, 6));

            var responses = await Task.WhenAll(
                s.Storekeeper.Client.PostAsync($"/api/stock-receipts/{a.Id}/approve", null),
                otherStorekeeper.Client.PostAsync($"/api/stock-receipts/{b.Id}/approve", null));

            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

            // Undo: cancel the approved one, delete the other draft.
            var admin = await AdminClientAsync();
            var approvedId = responses[0].IsSuccessStatusCode ? a.Id : b.Id;
            var draftId = approvedId == a.Id ? b.Id : a.Id;
            await ReadAsync<StockDocumentDto>(await PostAsync(admin, $"/api/stock-receipts/{approvedId}/cancel", new CancelStockDocumentRequest("Thử")));
            await s.Storekeeper.Client.DeleteAsync($"/api/stock-receipts/{draftId}");
        }

        Assert.Equal(0m, (await GetOrderAsync(s.Purchaser.Client, s.Order.Id)).Lines.Single().ReceivedQuantity);
    }

    [Fact]
    public async Task Overdue_lines_report_lists_what_is_still_due()
    {
        var s = await ApprovedOrderAsync(10, orderDate: Today.AddDays(-10), expectedDate: Today.AddDays(-3));
        var receipt = await CreateDraftAsync(s.Storekeeper.Client, "stock-receipts", ReceiptFor(s, 4));
        await ApproveAsync(s.Storekeeper.Client, "stock-receipts", receipt.Id);

        var overdue = await s.Purchaser.Client.GetFromJsonAsync<PagedResult<OverdueLineDto>>(
            $"/api/purchasing/reports/overdue-lines?supplierId={s.Order.Supplier.Id}", Json);
        var line = overdue!.Items.Single();
        Assert.Equal((s.Order.Number, 3, 10m, 4m, 6m), (line.OrderNumber, line.DaysLate, line.Ordered, line.Received, line.Remaining));

        var list = await s.Purchaser.Client.GetFromJsonAsync<PagedResult<PurchaseOrderSummaryDto>>(
            $"/api/purchase-orders?overdueOnly=true&supplierId={s.Order.Supplier.Id}", Json);
        Assert.True(list!.Items.Single().IsOverdue);

        var summary = await s.Purchaser.Client.GetFromJsonAsync<PurchasingSummaryDto>("/api/purchasing/reports/summary", Json);
        Assert.True(summary!.OverdueOrders >= 1);
        Assert.Contains(summary.Orders, o => o.Status == PurchaseOrderStatus.PartiallyReceived && o.Count >= 1);
        Assert.Contains(summary.Requests, r => r.Status == PurchaseRequestStatus.Ordered);

        var employee = await UserInAsync("SX", Roles.Employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.Client.GetAsync("/api/purchasing/reports/summary")).StatusCode);
    }
}
