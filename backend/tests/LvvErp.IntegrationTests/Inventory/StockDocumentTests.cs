using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Inventory;

public sealed class StockDocumentTests(ApiFactory factory) : InventoryTestBase(factory)
{
    [Fact]
    public async Task Receipt_changes_stock_only_when_approved_and_numbers_are_sequential()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);

        var first = await CreateDraftAsync(client, "stock-receipts",
            Receipt(site.Warehouse.Id, new ReceiptLineRequest(material.Id, site.Shelf1.Id, null, null, null, 10.5m)));
        var second = await CreateDraftAsync(client, "stock-receipts",
            Receipt(site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 1)));

        Assert.Matches($"^PN-{Today.Year}-\\d{{5}}$", first.Number);
        Assert.Equal(int.Parse(first.Number[^5..]) + 1, int.Parse(second.Number[^5..]));
        Assert.Equal(StockDocumentStatus.Draft, first.Status);
        Assert.Equal(0, await QuantityAsync(client, material.Id, site.Warehouse.Id, site.Shelf1.Id));

        var approved = await ApproveAsync(client, "stock-receipts", first.Id);

        Assert.Equal(StockDocumentStatus.Approved, approved.Status);
        Assert.NotNull(approved.ApprovedAt);
        Assert.False(string.IsNullOrEmpty(approved.ApprovedBy));
        Assert.Equal(10.5m, await QuantityAsync(client, material.Id, site.Warehouse.Id, site.Shelf1.Id));
        Assert.Single(await LedgerAsync(material.Id));
    }

    [Fact]
    public async Task Receipt_creates_lots_and_adds_to_existing_ones()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client, trackLots: true);
        var mfg = Today.AddDays(-30);
        var exp = Today.AddYears(1);

        var receipt = await ReceiveAsync(client, site.Warehouse.Id,
            new ReceiptLineRequest(material.Id, null, "L2026-01", mfg, exp, 20));
        Assert.Equal(("L2026-01", exp), (receipt.Lines[0].LotNumber, receipt.Lines[0].ExpiryDate));
        Assert.NotNull(receipt.Lines[0].LotId);

        await ReceiveAsync(client, site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, "L2026-01", null, null, 5));

        var lot = Assert.Single((await client.GetFromJsonAsync<List<LotDto>>($"/api/materials/{material.Id}/lots", Json))!);
        Assert.Equal(("L2026-01", mfg, exp, 25m), (lot.LotNumber, lot.ManufactureDate, lot.ExpiryDate, lot.Quantity));

        var conflictingDates = await PostAsync(client, "/api/stock-receipts", Receipt(site.Warehouse.Id,
            new ReceiptLineRequest(material.Id, null, "L2026-01", mfg, exp.AddDays(1), 1)));
        Assert.Equal(HttpStatusCode.BadRequest, conflictingDates.StatusCode);
    }

    [Fact]
    public async Task Approved_documents_are_immutable_and_drafts_can_be_deleted()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);
        var body = Receipt(site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 3));

        var approved = await ReceiveAsync(client, site.Warehouse.Id, body.Lines.ToArray());

        var edit = await client.PutAsJsonAsync($"/api/stock-receipts/{approved.Id}", body, Json);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("DocumentNotDraft", await ErrorCodeAsync(edit));
        Assert.Equal("DocumentNotDraft", await ErrorCodeAsync(await client.DeleteAsync($"/api/stock-receipts/{approved.Id}")));
        Assert.Equal("DocumentNotDraft", await ErrorCodeAsync(await client.PostAsync($"/api/stock-receipts/{approved.Id}/approve", null)));
        Assert.Equal(3, await QuantityAsync(client, material.Id, site.Warehouse.Id));

        var draft = await CreateDraftAsync(client, "stock-receipts", body);
        var updated = await ReadAsync<StockDocumentDto>(await client.PutAsJsonAsync($"/api/stock-receipts/{draft.Id}",
            body with { Note = "Sửa lại", Lines = [new ReceiptLineRequest(material.Id, site.Shelf2.Id, null, null, null, 7)] }, Json));
        Assert.Equal(("Sửa lại", 7m, "B-01"), (updated.Note, updated.Lines.Single().Quantity, updated.Lines.Single().LocationCode));
        Assert.Equal(draft.Number, updated.Number);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/stock-receipts/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/stock-receipts/{draft.Id}")).StatusCode);
    }

    [Fact]
    public async Task Issue_beyond_stock_is_rejected_with_details_and_changes_nothing()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var steel = await CreateMaterialAsync(client);
        var paint = await CreateMaterialAsync(client, trackLots: true);
        await ReceiveAsync(client, site.Warehouse.Id,
            new ReceiptLineRequest(steel.Id, site.Shelf1.Id, null, null, null, 10),
            new ReceiptLineRequest(paint.Id, null, "SON-01", null, null, 2));
        var paintLot = (await client.GetFromJsonAsync<List<LotDto>>($"/api/materials/{paint.Id}/lots", Json))!.Single();

        var issue = await CreateDraftAsync(client, "stock-issues", Issue(site.Warehouse.Id,
            new IssueLineRequest(steel.Id, site.Shelf1.Id, null, 4),
            new IssueLineRequest(paint.Id, null, paintLot.Id, 5),
            new IssueLineRequest(steel.Id, site.Shelf2.Id, null, 1)));

        var response = await client.PostAsync($"/api/stock-issues/{issue.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ProblemAsync(response);
        Assert.Equal("InsufficientStock", problem.GetProperty("errorCode").GetString());
        var details = problem.GetProperty("details").EnumerateArray().ToList();
        Assert.Equal(2, details.Count);
        Assert.Equal((2, "SON-01", 5m, 2m), (details[0].GetProperty("lineNo").GetInt32(), details[0].GetProperty("lotNumber").GetString(),
            details[0].GetProperty("requested").GetDecimal(), details[0].GetProperty("available").GetDecimal()));
        Assert.Equal((3, 0m), (details[1].GetProperty("lineNo").GetInt32(), details[1].GetProperty("available").GetDecimal()));

        // All or nothing: line 1 would have fitted but was not posted either.
        Assert.Equal(10, await QuantityAsync(client, steel.Id, site.Warehouse.Id, site.Shelf1.Id));
        Assert.Equal(StockDocumentStatus.Draft,
            (await client.GetFromJsonAsync<StockDocumentDto>($"/api/stock-issues/{issue.Id}", Json))!.Status);
    }

    [Fact]
    public async Task Issue_and_transfers_move_stock()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var a = await CreateWarehouseAsync(client);
        var b = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);
        await ReceiveAsync(client, a.Warehouse.Id, new ReceiptLineRequest(material.Id, a.Shelf1.Id, null, null, null, 10));

        var issue = await CreateDraftAsync(client, "stock-issues", Issue(a.Warehouse.Id, new IssueLineRequest(material.Id, a.Shelf1.Id, null, 3)));
        await ApproveAsync(client, "stock-issues", issue.Id);
        Assert.Equal(7, await QuantityAsync(client, material.Id, a.Warehouse.Id, a.Shelf1.Id));

        var transfer = await CreateDraftAsync(client, "stock-transfers", new SaveStockTransferRequest(Today, a.Warehouse.Id, b.Warehouse.Id, null,
            [new TransferLineRequest(material.Id, null, a.Shelf1.Id, b.Shelf2.Id, 4)]));
        Assert.Matches("^PC-", transfer.Number);
        await ApproveAsync(client, "stock-transfers", transfer.Id);
        Assert.Equal(3, await QuantityAsync(client, material.Id, a.Warehouse.Id, a.Shelf1.Id));
        Assert.Equal(4, await QuantityAsync(client, material.Id, b.Warehouse.Id, b.Shelf2.Id));

        // Moving between locations of one warehouse is a transfer too.
        var move = await CreateDraftAsync(client, "stock-transfers", new SaveStockTransferRequest(Today, b.Warehouse.Id, b.Warehouse.Id, null,
            [new TransferLineRequest(material.Id, null, b.Shelf2.Id, b.Shelf1.Id, 1)]));
        await ApproveAsync(client, "stock-transfers", move.Id);
        Assert.Equal(3, await QuantityAsync(client, material.Id, b.Warehouse.Id, b.Shelf2.Id));
        Assert.Equal(1, await QuantityAsync(client, material.Id, b.Warehouse.Id, b.Shelf1.Id));

        Assert.Equal(7, (await LedgerAsync(material.Id)).Sum(t => t.Quantity));
    }

    [Fact]
    public async Task Lot_location_and_date_rules_are_enforced()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var a = await CreateWarehouseAsync(client);
        var b = await CreateWarehouseAsync(client);
        var plain = await CreateMaterialAsync(client);
        var tracked = await CreateMaterialAsync(client, trackLots: true);
        var other = await CreateMaterialAsync(client, trackLots: true);
        await ReceiveAsync(client, a.Warehouse.Id, new ReceiptLineRequest(other.Id, null, "X-1", null, null, 1));
        var otherLot = (await client.GetFromJsonAsync<List<LotDto>>($"/api/materials/{other.Id}/lots", Json))!.Single();

        async Task AssertBad(string resource, object body, string field)
        {
            var response = await PostAsync(client, $"/api/{resource}", body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await ProblemAsync(response)).GetProperty("errors").TryGetProperty(field, out _),
                $"expected an error on {field}: {await response.Content.ReadAsStringAsync()}");
        }

        await AssertBad("stock-receipts", Receipt(a.Warehouse.Id, new ReceiptLineRequest(tracked.Id, null, null, null, null, 1)), "Lines[0].LotNumber");
        await AssertBad("stock-receipts", Receipt(a.Warehouse.Id, new ReceiptLineRequest(plain.Id, null, "L1", null, null, 1)), "Lines[0].LotNumber");
        await AssertBad("stock-receipts", Receipt(a.Warehouse.Id, new ReceiptLineRequest(plain.Id, b.Shelf1.Id, null, null, null, 1)), "Lines[0].LocationId");
        await AssertBad("stock-issues", Issue(a.Warehouse.Id, new IssueLineRequest(tracked.Id, null, null, 1)), "Lines[0].LotId");
        await AssertBad("stock-issues", Issue(a.Warehouse.Id, new IssueLineRequest(tracked.Id, null, otherLot.Id, 1)), "Lines[0].LotId");
        await AssertBad("stock-receipts", Receipt(a.Warehouse.Id, new ReceiptLineRequest(plain.Id, null, null, null, null, 1)) with
            { DocumentDate = Today.AddDays(1) }, "DocumentDate");
        await AssertBad("stock-issues", Issue(a.Warehouse.Id, new IssueLineRequest(plain.Id, null, null, 1)) with
            { Reason = StockDocumentReason.Purchase }, "Reason");
        await AssertBad("stock-transfers", new SaveStockTransferRequest(Today, a.Warehouse.Id, a.Warehouse.Id, null,
            [new TransferLineRequest(plain.Id, null, a.Shelf1.Id, a.Shelf1.Id, 1)]), "Lines[0]");
    }

    [Fact]
    public async Task Concurrent_issues_never_drive_stock_negative()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);
        await ReceiveAsync(client, site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 10));

        var drafts = new List<StockDocumentDto>();
        for (var i = 0; i < 4; i++)
            drafts.Add(await CreateDraftAsync(client, "stock-issues", Issue(site.Warehouse.Id, new IssueLineRequest(material.Id, null, null, 7))));

        var clients = await Task.WhenAll(drafts.Select(_ => ClientWithRoleAsync(Roles.Storekeeper)));
        var responses = await Task.WhenAll(drafts.Select((d, i) => clients[i].PostAsync($"/api/stock-issues/{d.Id}/approve", null)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(3, await QuantityAsync(client, material.Id, site.Warehouse.Id));
        Assert.Equal(3, (await LedgerAsync(material.Id)).Sum(t => t.Quantity));
    }

    [Fact]
    public async Task Approving_the_same_document_twice_at_once_posts_it_once()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);
        var draft = await CreateDraftAsync(client, "stock-receipts",
            Receipt(site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 5)));

        var responses = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => client.PostAsync($"/api/stock-receipts/{draft.Id}/approve", null)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(5, await QuantityAsync(client, material.Id, site.Warehouse.Id));
        Assert.Single(await LedgerAsync(material.Id));
    }

    [Fact]
    public async Task Cancelling_reverses_stock_keeps_history_and_is_admin_only()
    {
        var storekeeper = await ClientWithRoleAsync(Roles.Storekeeper);
        var admin = await AdminClientAsync();
        var site = await CreateWarehouseAsync(storekeeper);
        var material = await CreateMaterialAsync(storekeeper);
        var receipt = await ReceiveAsync(storekeeper, site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 10));
        var issue = await CreateDraftAsync(storekeeper, "stock-issues", Issue(site.Warehouse.Id, new IssueLineRequest(material.Id, null, null, 8)));

        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(admin, $"/api/stock-issues/{issue.Id}/cancel",
            new CancelStockDocumentRequest("Nháp"))).StatusCode); // drafts are deleted, not cancelled
        await ApproveAsync(storekeeper, "stock-issues", issue.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(storekeeper, $"/api/stock-receipts/{receipt.Id}/cancel",
            new CancelStockDocumentRequest("Nhập nhầm"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(admin, $"/api/stock-receipts/{receipt.Id}/cancel",
            new CancelStockDocumentRequest("  "))).StatusCode);

        // 8 of the 10 received have been issued, so the receipt can't be undone yet.
        var blocked = await PostAsync(admin, $"/api/stock-receipts/{receipt.Id}/cancel", new CancelStockDocumentRequest("Nhập nhầm"));
        Assert.Equal("InsufficientStock", await ErrorCodeAsync(blocked));

        var cancelledIssue = await ReadAsync<StockDocumentDto>(await PostAsync(admin, $"/api/stock-issues/{issue.Id}/cancel",
            new CancelStockDocumentRequest("Xuất nhầm kho")));
        Assert.Equal((StockDocumentStatus.Cancelled, "Xuất nhầm kho"), (cancelledIssue.Status, cancelledIssue.CancelReason));
        Assert.Equal(10, await QuantityAsync(storekeeper, material.Id, site.Warehouse.Id));

        await ReadAsync<StockDocumentDto>(await PostAsync(admin, $"/api/stock-receipts/{receipt.Id}/cancel",
            new CancelStockDocumentRequest("Nhập nhầm")));
        Assert.Equal(0, await QuantityAsync(storekeeper, material.Id, site.Warehouse.Id));

        var ledger = await LedgerAsync(material.Id);
        Assert.Equal([10m, -8m, 8m, -10m], ledger.Select(t => t.Quantity));
        Assert.Equal([false, false, true, true], ledger.Select(t => t.IsReversal));
        Assert.All(ledger.Where(t => t.IsReversal), t => Assert.Equal(Today, t.TransactionDate));
        Assert.Equal("DocumentNotApproved", await ErrorCodeAsync(await PostAsync(admin, $"/api/stock-issues/{issue.Id}/cancel",
            new CancelStockDocumentRequest("Lần nữa"))));
    }

    [Theory]
    [InlineData(Roles.HR)]
    [InlineData(Roles.Employee)]
    public async Task Other_roles_can_view_documents_but_not_write(string role)
    {
        var storekeeper = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(storekeeper);
        var material = await CreateMaterialAsync(storekeeper);
        var draft = await CreateDraftAsync(storekeeper, "stock-receipts",
            Receipt(site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 1)));
        var client = await ClientWithRoleAsync(role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/stock-receipts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/stock-receipts/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/stock/available?materialId={material.Id}&warehouseId={site.Warehouse.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(client, "/api/stock-receipts",
            Receipt(site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 1)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/stock-receipts/{draft.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/stock-receipts/{draft.Id}")).StatusCode);
    }

    [Fact]
    public async Task Issue_records_recipient_and_lists_filter_and_search()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var hr = await ClientWithRoleAsync(Roles.HR);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);
        await ReceiveAsync(client, site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 5));
        var token = Guid.NewGuid().ToString("N")[..6];
        var employee = await CreateEmployeeAsync(hr, await NewEmployeeRequestAsync(hr, fullName: $"Trần Văn Nhận {token}"));

        var issue = await CreateDraftAsync(client, "stock-issues", Issue(site.Warehouse.Id, new IssueLineRequest(material.Id, null, null, 2)) with
            { DepartmentId = employee.DepartmentId, EmployeeId = employee.Id });
        Assert.Equal(("Sản xuất", employee.FullName), (issue.DepartmentName, issue.EmployeeName));

        var byRecipient = await client.GetFromJsonAsync<PagedResult<StockDocumentSummaryDto>>(
            $"/api/stock-issues?search={Uri.EscapeDataString($"tran van nhan {token}")}", Json);
        Assert.Equal(issue.Id, Assert.Single(byRecipient!.Items).Id);

        var drafts = await client.GetFromJsonAsync<PagedResult<StockDocumentSummaryDto>>(
            $"/api/stock-issues?warehouseId={site.Warehouse.Id}&status=Draft&from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", Json);
        Assert.Equal(issue.Id, Assert.Single(drafts!.Items).Id);
        var receipts = await client.GetFromJsonAsync<PagedResult<StockDocumentSummaryDto>>(
            $"/api/stock-receipts?warehouseId={site.Warehouse.Id}&status=Approved", Json);
        Assert.Single(receipts!.Items);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/stock-documents/{issue.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/stock-receipts/{issue.Id}")).StatusCode);
    }

    [Fact]
    public async Task Lot_tracking_is_locked_once_a_material_appears_on_a_document()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);
        await CreateDraftAsync(client, "stock-receipts",
            Receipt(site.Warehouse.Id, new ReceiptLineRequest(material.Id, null, null, null, null, 1)));

        var response = await client.PutAsJsonAsync($"/api/materials/{material.Id}", new SaveMaterialRequest(
            material.Code, material.Name, null, material.UnitId, material.GroupId, 0, TrackLots: true), Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TrackLotsLocked", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Used_warehouse_and_location_cannot_be_deleted()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var material = await CreateMaterialAsync(client);
        await ReceiveAsync(client, site.Warehouse.Id, new ReceiptLineRequest(material.Id, site.Shelf1.Id, null, null, null, 1));

        Assert.Equal("InUse", await ErrorCodeAsync(await client.DeleteAsync($"/api/locations/{site.Shelf1.Id}")));
        Assert.Equal("InUse", await ErrorCodeAsync(await client.DeleteAsync($"/api/warehouses/{site.Warehouse.Id}")));
        Assert.Equal("InUse", await ErrorCodeAsync(await client.DeleteAsync($"/api/materials/{material.Id}")));
    }
}
