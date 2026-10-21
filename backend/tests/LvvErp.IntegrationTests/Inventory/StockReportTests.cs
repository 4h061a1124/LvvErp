using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Application.Features.Inventory.Reports;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Inventory;

public sealed class StockReportTests(ApiFactory factory) : InventoryTestBase(factory)
{
    [Fact]
    public async Task On_hand_shows_quantity_per_warehouse_against_the_total_minimum()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var a = await CreateWarehouseAsync(client);
        var b = await CreateWarehouseAsync(client);
        var token = Guid.NewGuid().ToString("N")[..8];
        var low = await CreateMaterialAsync(client, name: $"Que hàn {token}", minStock: 50);
        var empty = await CreateMaterialAsync(client, name: $"Đá cắt {token}");
        await ReceiveAsync(client, a.Warehouse.Id, new ReceiptLineRequest(low.Id, a.Shelf1.Id, null, null, null, 30));
        await ReceiveAsync(client, b.Warehouse.Id, new ReceiptLineRequest(low.Id, null, null, null, null, 5));

        var all = (await client.GetFromJsonAsync<PagedResult<StockOnHandDto>>($"/api/stock/on-hand?search={token}", Json))!;
        var row = Assert.Single(all.Items);
        Assert.Equal((low.Id, 35m, 35m, true, "Kg"), (row.MaterialId, row.Quantity, row.TotalQuantity, row.IsBelowMin, row.UnitName));

        var inB = (await client.GetFromJsonAsync<PagedResult<StockOnHandDto>>($"/api/stock/on-hand?search={token}&warehouseId={b.Warehouse.Id}", Json))!;
        Assert.Equal((5m, 35m), (inB.Items.Single().Quantity, inB.Items.Single().TotalQuantity));

        var withZero = (await client.GetFromJsonAsync<PagedResult<StockOnHandDto>>($"/api/stock/on-hand?search={token}&includeZero=true&sort=name", Json))!;
        Assert.Equal([empty.Id, low.Id], withZero.Items.Select(i => i.MaterialId));

        var belowMin = (await client.GetFromJsonAsync<PagedResult<StockOnHandDto>>($"/api/stock/on-hand?search={token}&belowMinOnly=true", Json))!;
        Assert.Equal(low.Id, Assert.Single(belowMin.Items).MaterialId);

        var groups = (await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/material-groups", Json))!;
        var otherGroup = (await client.GetFromJsonAsync<PagedResult<StockOnHandDto>>(
            $"/api/stock/on-hand?search={token}&groupId={groups.Single(g => g.Code == "BB").Id}", Json))!;
        Assert.Empty(otherGroup.Items);

        var detail = (await client.GetFromJsonAsync<List<StockOnHandDetailDto>>($"/api/stock/on-hand/{low.Id}", Json))!;
        Assert.Equal(2, detail.Count);
        Assert.Contains(detail, d => d.WarehouseId == a.Warehouse.Id && d.LocationCode == "A-01" && d.Quantity == 30);
        Assert.Contains(detail, d => d.WarehouseId == b.Warehouse.Id && d.LocationCode == null && d.Quantity == 5);
    }

    [Fact]
    public async Task Below_min_list_reports_the_shortage()
    {
        var client = await ClientWithRoleAsync(Roles.Employee);
        var storekeeper = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(storekeeper);
        var critical = await CreateMaterialAsync(storekeeper, minStock: 100);
        var fine = await CreateMaterialAsync(storekeeper, minStock: 10);
        await ReceiveAsync(storekeeper, site.Warehouse.Id,
            new ReceiptLineRequest(critical.Id, null, null, null, null, 40),
            new ReceiptLineRequest(fine.Id, null, null, null, null, 10));

        var list = (await client.GetFromJsonAsync<List<BelowMinStockDto>>("/api/stock/below-min", Json))!;

        var row = Assert.Single(list, r => r.MaterialId == critical.Id);
        Assert.Equal((40m, 100m, 60m), (row.Quantity, row.MinStock, row.Shortage));
        Assert.DoesNotContain(list, r => r.MaterialId == fine.Id);
    }

    [Fact]
    public async Task Stock_card_has_opening_running_balance_transfers_and_reversals()
    {
        var storekeeper = await ClientWithRoleAsync(Roles.Storekeeper);
        var admin = await AdminClientAsync();
        var viewer = await ClientWithRoleAsync(Roles.Employee);
        var a = await CreateWarehouseAsync(storekeeper);
        var b = await CreateWarehouseAsync(storekeeper);
        var material = await CreateMaterialAsync(storekeeper);

        var opening = await CreateDraftAsync(storekeeper, "stock-receipts",
            Receipt(a.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 20)) with { DocumentDate = Today.AddDays(-10) });
        await ApproveAsync(storekeeper, "stock-receipts", opening.Id);
        var issue = await CreateDraftAsync(storekeeper, "stock-issues",
            Issue(a.Warehouse.Id, new IssueLineRequest(material.Id, null, null, 6)) with { DocumentDate = Today.AddDays(-5) });
        await ApproveAsync(storekeeper, "stock-issues", issue.Id);
        var transfer = await CreateDraftAsync(storekeeper, "stock-transfers", new SaveStockTransferRequest(
            Today.AddDays(-2), a.Warehouse.Id, b.Warehouse.Id, null, [new TransferLineRequest(material.Id, null, null, null, 4)]));
        await ApproveAsync(storekeeper, "stock-transfers", transfer.Id);
        var receipt = await ReceiveAsync(storekeeper, a.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 10));
        await ReadAsync<StockDocumentDto>(await PostAsync(admin, $"/api/stock-issues/{issue.Id}/cancel", new CancelStockDocumentRequest("Xuất nhầm")));

        // All warehouses, from 7 days ago: the first receipt becomes the opening balance.
        var card = (await viewer.GetFromJsonAsync<StockCardDto>(
            $"/api/stock/card?materialId={material.Id}&from={Today.AddDays(-7):yyyy-MM-dd}", Json))!;
        Assert.Equal((20m, 20m, 10m, 30m), (card.Opening, card.TotalIn, card.TotalOut, card.Closing));
        Assert.Equal([14m, 10m, 14m, 24m, 30m], card.Entries.Select(e => e.Balance));
        Assert.Equal([issue.Number, transfer.Number, transfer.Number, receipt.Number, issue.Number], card.Entries.Select(e => e.DocumentNumber));
        Assert.True(card.Entries[^1].IsReversal);
        Assert.Equal(Today, card.Entries[^1].Date);

        // One warehouse: the transfer is a single outgoing line naming the other side.
        var cardA = (await viewer.GetFromJsonAsync<StockCardDto>(
            $"/api/stock/card?materialId={material.Id}&warehouseId={a.Warehouse.Id}&from={Today.AddDays(-7):yyyy-MM-dd}", Json))!;
        Assert.Equal([14m, 10m, 20m, 26m], cardA.Entries.Select(e => e.Balance));
        Assert.Equal((4m, b.Warehouse.Name), (cardA.Entries[1].Out, cardA.Entries[1].OtherWarehouseName));
        Assert.Equal(26m, cardA.Closing);
        Assert.Equal(26m, await QuantityAsync(storekeeper, material.Id, a.Warehouse.Id));

        var untilThreeDaysAgo = (await viewer.GetFromJsonAsync<StockCardDto>(
            $"/api/stock/card?materialId={material.Id}&to={Today.AddDays(-3):yyyy-MM-dd}", Json))!;
        Assert.Equal((0m, 2, 14m), (untilThreeDaysAgo.Opening, untilThreeDaysAgo.Entries.Count, untilThreeDaysAgo.Closing));

        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/stock/card?materialId={Guid.NewGuid()}")).StatusCode);
    }
}
