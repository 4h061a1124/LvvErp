using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Purchasing.Approvals;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Purchasing;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Purchasing;

public sealed class PurchaseOrderTests(ApiFactory factory) : PurchasingTestBase(factory)
{
    private sealed record Setup(Actor Purchaser, Guid SupplierId, Guid WarehouseId, Guid MaterialId);

    private async Task<Setup> SetupAsync()
    {
        var admin = await AdminClientAsync();
        var purchaser = await PurchaserAsync();
        var supplier = await CreateSupplierAsync(purchaser.Client);
        var site = await CreateWarehouseAsync(admin);
        var material = await CreateMaterialAsync(admin);
        return new Setup(purchaser, supplier.Id, site.Warehouse.Id, material.Id);
    }

    [Fact]
    public async Task Order_combines_requests_and_updates_their_status()
    {
        var s = await SetupAsync();
        var first = await ApprovedRequestAsync((s.MaterialId, 10));
        var second = await ApprovedRequestAsync((s.MaterialId, 5));
        var line1 = first.Lines.Single();
        var line2 = second.Lines.Single();

        var orderable = await s.Purchaser.Client.GetFromJsonAsync<PagedResult<OrderableRequestLineDto>>(
            $"/api/purchase-requests/orderable-lines?materialId={s.MaterialId}", Json);
        Assert.Equal(new[] { line1.Id, line2.Id }.Order(), orderable!.Items.Select(l => l.RequestLineId).Order());

        // 12 of 15: first request fully covered, second partly.
        var order = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders", NewOrder(s.SupplierId, s.WarehouseId,
            new PurchaseOrderLineRequest(s.MaterialId, 12, 1234.56m, [line1.Id, line2.Id])));
        Assert.Matches(@"^DH-\d{4}-\d{5}$", order.Number);
        var line = order.Lines.Single();
        Assert.Equal([(first.Number, 10m), (second.Number, 2m)],
            line.Sources.OrderBy(x => x.RequestNumber).Select(x => (x.RequestNumber, x.Quantity)));
        Assert.Equal((14_815m, 14_815m, 1_482m, 16_297m), (line.Amount, order.Subtotal, order.VatAmount, order.Total));

        Assert.Equal(PurchaseRequestStatus.Ordered, (await GetRequestAsync(s.Purchaser.Client, first.Id)).Status);
        var secondNow = await GetRequestAsync(s.Purchaser.Client, second.Id);
        Assert.Equal((PurchaseRequestStatus.PartiallyOrdered, 2m), (secondNow.Status, secondNow.Lines.Single().OrderedQuantity));
        Assert.Equal(order.Number, secondNow.Orders.Single().Number);

        var remaining = await s.Purchaser.Client.GetFromJsonAsync<PagedResult<OrderableRequestLineDto>>(
            $"/api/purchase-requests/orderable-lines?materialId={s.MaterialId}", Json);
        Assert.Equal(3m, remaining!.Items.Single().RemainingQuantity);

        // Editing the order down releases the quantity again.
        var edited = await ReadAsync<PurchaseOrderDto>(await s.Purchaser.Client.PutAsJsonAsync($"/api/purchase-orders/{order.Id}",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(line1, quantity: 4)), Json));
        Assert.Equal(4m, edited.Lines.Single().Sources.Single().Quantity);
        Assert.Equal(PurchaseRequestStatus.PartiallyOrdered, (await GetRequestAsync(s.Purchaser.Client, first.Id)).Status);
        Assert.Equal(PurchaseRequestStatus.Approved, (await GetRequestAsync(s.Purchaser.Client, second.Id)).Status);
    }

    [Fact]
    public async Task Ordering_more_than_requested_needs_a_reason()
    {
        var s = await SetupAsync();
        var request = await ApprovedRequestAsync((s.MaterialId, 10));
        var source = request.Lines.Single();

        var noReason = await s.Purchaser.Client.PostAsJsonAsync("/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 12)), Json);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.True((await ProblemAsync(noReason)).GetProperty("errors").TryGetProperty("Lines[0].OverOrderReason", out _));

        var order = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 12, overOrderReason: "NCC bán theo thùng 12")));
        Assert.Equal(("NCC bán theo thùng 12", 10m), (order.Lines.Single().OverOrderReason, order.Lines.Single().Sources.Single().Quantity));
        Assert.Equal(PurchaseRequestStatus.Ordered, (await GetRequestAsync(s.Purchaser.Client, request.Id)).Status);

        // A fully ordered request can't be put on a new order.
        var second = await s.Purchaser.Client.PostAsJsonAsync("/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 1)), Json);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Small_order_is_approved_by_the_purchasing_head_and_large_one_by_a_director()
    {
        var s = await SetupAsync();
        var head = await PurchasingHeadAsync();
        var director = await DirectorAsync();
        var request = await ApprovedRequestAsync((s.MaterialId, 100));
        var source = request.Lines.Single();

        var small = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 10, unitPrice: 100_000)));
        small = await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{small.Id}/submit");
        Assert.Equal(PurchaseOrderApprovalLevel.PurchasingHead, small.ApprovalLevel);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Purchaser.Client.PostAsync($"/api/purchase-orders/{small.Id}/approve", null)).StatusCode);
        Assert.Contains((await head.Client.GetFromJsonAsync<ApprovalInboxDto>("/api/purchasing/approvals", Json))!.Orders, o => o.Id == small.Id);
        var approved = await PostOrderAsync(head.Client, $"/api/purchase-orders/{small.Id}/approve");
        Assert.Equal((PurchaseOrderStatus.Approved, head.User.UserName), (approved.Status, approved.ApprovedBy));

        // 90 × 555,556 = 50,000,040 before VAT: at or above 50 million → director.
        var large = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 90, unitPrice: 555_556)));
        large = await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{large.Id}/submit");
        Assert.Equal(PurchaseOrderApprovalLevel.Director, large.ApprovalLevel);
        Assert.DoesNotContain((await head.Client.GetFromJsonAsync<ApprovalInboxDto>("/api/purchasing/approvals", Json))!.Orders, o => o.Id == large.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.Client.PostAsync($"/api/purchase-orders/{large.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await AdminClientAsync()).PostAsync($"/api/purchase-orders/{large.Id}/approve", null)).StatusCode);

        Assert.True((await GetOrderAsync(director.Client, large.Id)).Actions.CanApprove);
        Assert.Equal(PurchaseOrderStatus.Approved, (await PostOrderAsync(director.Client, $"/api/purchase-orders/{large.Id}/approve")).Status);
    }

    [Fact]
    public async Task Head_of_purchasing_cannot_approve_an_order_they_created()
    {
        var s = await SetupAsync();
        var headWhoBuys = await UserInAsync("MH", Roles.DepartmentHead, Roles.Purchaser);
        var request = await ApprovedRequestAsync((s.MaterialId, 1));

        var order = await PostOrderAsync(headWhoBuys.Client, "/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(request.Lines.Single())));
        await PostOrderAsync(headWhoBuys.Client, $"/api/purchase-orders/{order.Id}/submit");

        Assert.False((await GetOrderAsync(headWhoBuys.Client, order.Id)).Actions.CanApprove);
        Assert.Equal(HttpStatusCode.Forbidden, (await headWhoBuys.Client.PostAsync($"/api/purchase-orders/{order.Id}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Rejected_order_can_be_edited_and_resubmitted_and_cancel_releases_the_request()
    {
        var s = await SetupAsync();
        var head = await PurchasingHeadAsync();
        var request = await ApprovedRequestAsync((s.MaterialId, 10));
        var source = request.Lines.Single();
        var order = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders", NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source)));
        await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{order.Id}/submit");

        var rejected = await PostOrderAsync(head.Client, $"/api/purchase-orders/{order.Id}/reject", Reason("Giá cao"));
        Assert.Equal((PurchaseOrderStatus.Rejected, "Giá cao"), (rejected.Status, rejected.RejectReason));

        var edited = await ReadAsync<PurchaseOrderDto>(await s.Purchaser.Client.PutAsJsonAsync($"/api/purchase-orders/{order.Id}",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, unitPrice: 9_000)), Json));
        Assert.Equal((PurchaseOrderStatus.Draft, (PurchaseOrderApprovalLevel?)null), (edited.Status, edited.ApprovalLevel));
        await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{order.Id}/submit");

        // Request is fully on the order: it can be neither cancelled nor closed.
        var cancelRequest = await s.Purchaser.Client.PostAsJsonAsync($"/api/purchase-requests/{request.Id}/cancel", Reason(), Json);
        Assert.Equal(ErrorCodes.InvalidStatus, await ErrorCodeAsync(cancelRequest)); // Ordered: not cancellable
        var closeRequest = await s.Purchaser.Client.PostAsJsonAsync($"/api/purchase-requests/{request.Id}/close", Reason(), Json);
        Assert.Equal(HttpStatusCode.Conflict, closeRequest.StatusCode);

        var cancelled = await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{order.Id}/cancel", Reason("NCC hết hàng"));
        Assert.Equal(PurchaseOrderStatus.Cancelled, cancelled.Status);
        var released = await GetRequestAsync(s.Purchaser.Client, request.Id);
        Assert.Equal((PurchaseRequestStatus.Approved, 0m), (released.Status, released.Lines.Single().OrderedQuantity));
    }

    [Fact]
    public async Task Request_with_an_open_order_cannot_be_closed_until_the_order_is_approved()
    {
        var s = await SetupAsync();
        var head = await PurchasingHeadAsync();
        var request = await ApprovedRequestAsync((s.MaterialId, 10));
        var order = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(request.Lines.Single(), quantity: 6)));

        var blocked = await s.Purchaser.Client.PostAsJsonAsync($"/api/purchase-requests/{request.Id}/close", Reason(), Json);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(ErrorCodes.RequestHasOpenOrders, await ErrorCodeAsync(blocked));
        var cancelBlocked = await s.Purchaser.Client.PostAsJsonAsync($"/api/purchase-requests/{request.Id}/cancel", Reason(), Json);
        Assert.Equal(HttpStatusCode.Conflict, cancelBlocked.StatusCode);

        await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{order.Id}/submit");
        await PostOrderAsync(head.Client, $"/api/purchase-orders/{order.Id}/approve");

        var closed = await PostRequestAsync(s.Purchaser.Client, $"/api/purchase-requests/{request.Id}/close", Reason("Đủ dùng"));
        Assert.Equal(PurchaseRequestStatus.Closed, closed.Status);
        var orderable = await s.Purchaser.Client.GetFromJsonAsync<PagedResult<OrderableRequestLineDto>>(
            $"/api/purchase-requests/orderable-lines?materialId={s.MaterialId}", Json);
        Assert.Empty(orderable!.Items);
    }

    [Fact]
    public async Task Concurrent_orders_cannot_both_take_what_a_request_still_needs()
    {
        // Edits, not creates: creating takes the number-sequence row lock, which would serialize the
        // two requests even without the purchase request lock this test is about.
        var s = await SetupAsync();
        var otherPurchaser = await PurchaserAsync();
        var request = await ApprovedRequestAsync((s.MaterialId, 10));
        var source = request.Lines.Single();
        var small = NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 1));
        var big = NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 9));
        var a = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders", small);
        var b = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders", small);

        for (var round = 0; round < 5; round++)
        {
            var responses = await Task.WhenAll(
                s.Purchaser.Client.PutAsJsonAsync($"/api/purchase-orders/{a.Id}", big, Json),
                otherPurchaser.Client.PutAsJsonAsync($"/api/purchase-orders/{b.Id}", big, Json));

            // 9 + 9 > 10: whoever goes second over-orders without a reason.
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

            foreach (var id in new[] { a.Id, b.Id })
                await ReadAsync<PurchaseOrderDto>(await s.Purchaser.Client.PutAsJsonAsync($"/api/purchase-orders/{id}", small, Json));
        }

        var final = await GetRequestAsync(s.Purchaser.Client, request.Id);
        Assert.Equal((PurchaseRequestStatus.PartiallyOrdered, 2m), (final.Status, final.Lines.Single().OrderedQuantity));
    }

    [Fact]
    public async Task Approved_prices_feed_last_price_and_history()
    {
        var s = await SetupAsync();
        var head = await PurchasingHeadAsync();
        var otherSupplier = await CreateSupplierAsync(s.Purchaser.Client);
        var request = await ApprovedRequestAsync((s.MaterialId, 30));
        var source = request.Lines.Single();

        async Task<PurchaseOrderDto> ApprovedOrderAsync(Guid supplierId, decimal price)
        {
            var order = await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders",
                NewOrder(supplierId, s.WarehouseId, OrderLine(source, quantity: 10, unitPrice: price)));
            await PostOrderAsync(s.Purchaser.Client, $"/api/purchase-orders/{order.Id}/submit");
            return await PostOrderAsync(head.Client, $"/api/purchase-orders/{order.Id}/approve");
        }

        await ApprovedOrderAsync(s.SupplierId, 11_000);
        await ApprovedOrderAsync(otherSupplier.Id, 10_500);
        // A draft price is not an agreed price.
        await PostOrderAsync(s.Purchaser.Client, "/api/purchase-orders",
            NewOrder(s.SupplierId, s.WarehouseId, OrderLine(source, quantity: 10, unitPrice: 1)));

        var forSupplier = await s.Purchaser.Client.GetFromJsonAsync<List<LastPriceDto>>(
            $"/api/purchase-orders/last-prices?materialIds={s.MaterialId}&supplierId={s.SupplierId}", Json);
        var price = forSupplier!.Single();
        Assert.Equal((11_000m, true), (price.UnitPrice, price.SameSupplier));

        var newSupplier = await CreateSupplierAsync(s.Purchaser.Client);
        var fallback = await s.Purchaser.Client.GetFromJsonAsync<List<LastPriceDto>>(
            $"/api/purchase-orders/last-prices?materialIds={s.MaterialId}&supplierId={newSupplier.Id}", Json);
        Assert.False(fallback!.Single().SameSupplier);

        var history = await s.Purchaser.Client.GetFromJsonAsync<PagedResult<PriceHistoryDto>>(
            $"/api/purchase-orders/price-history?materialId={s.MaterialId}", Json);
        Assert.Equal([10_500m, 11_000m], history!.Items.Select(h => h.UnitPrice).Order());
    }

    [Fact]
    public async Task Roles_without_order_permissions_get_403()
    {
        var s = await SetupAsync();
        var request = await ApprovedRequestAsync((s.MaterialId, 1));
        var storekeeper = await UserInAsync("KHO", Roles.Storekeeper);
        var employee = await UserInAsync("SX", Roles.Employee);
        var body = NewOrder(s.SupplierId, s.WarehouseId, OrderLine(request.Lines.Single()));

        Assert.Equal(HttpStatusCode.OK, (await storekeeper.Client.GetAsync("/api/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.Client.PostAsJsonAsync("/api/purchase-orders", body, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.Client.GetAsync("/api/purchase-requests/orderable-lines")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.Client.GetAsync("/api/purchase-orders")).StatusCode);
    }
}
