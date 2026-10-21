using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Application.Features.Inventory.Import;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Inventory;

public sealed class InventoryImportTests(ApiFactory factory) : InventoryTestBase(factory)
{
    private const string MaterialTemplate = "/api/materials/import/template";
    private const string OpeningTemplate = "/api/stock/opening-balance/template";

    private static async Task<MaterialDto?> FindMaterialAsync(HttpClient client, string code) =>
        (await client.GetFromJsonAsync<PagedResult<MaterialDto>>($"/api/materials?search={code}", Json))!.Items.SingleOrDefault(m => m.Code == code);

    [Fact]
    public async Task Material_import_creates_and_updates_matching_units_and_groups_loosely()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        string bolt = UniqueCode(), oil = UniqueCode();

        var file = await FillTemplateAsync(client, MaterialTemplate,
            [bolt.ToLowerInvariant(), "Bu lông lục giác", "M8x30", "cai", "PT", "100", "Không"],
            [oil, "Dầu thủy lực", null, "Lít", "vat tu tieu hao", null, "co"]);
        var result = await ReadAsync<ImportResult>(await UploadAsync(client, "/api/materials/import", file));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => $"{e.Row}/{e.Column}: {e.Message}")));
        Assert.Equal((2, 0), (result.Created, result.Updated));
        var created = await FindMaterialAsync(client, bolt);
        Assert.Equal(("Cái", "Phụ tùng thay thế", 100m, false, "M8x30"),
            (created!.UnitName, created.GroupName, created.MinStock, created.TrackLots, created.Specification));
        Assert.True((await FindMaterialAsync(client, oil))!.TrackLots);

        // Existing code: rejected by default, updated with the option; blank cells keep values.
        var update = await FillTemplateAsync(client, MaterialTemplate, [bolt, "Bu lông inox", null, "CAI", "PT", null, null]);
        var rejected = await ReadAsync<ImportResult>(await UploadAsync(client, "/api/materials/import", update));
        Assert.Equal(ImportErrorCodes.AlreadyExists, Assert.Single(rejected.Errors).ErrorCode);

        var updated = await ReadAsync<ImportResult>(await UploadAsync(client, "/api/materials/import?updateExisting=true", update));
        Assert.Equal((0, 1), (updated.Created, updated.Updated));
        var after = await FindMaterialAsync(client, bolt);
        Assert.Equal(("Bu lông inox", "M8x30", 100m), (after!.Name, after.Specification, after.MinStock));
    }

    [Fact]
    public async Task Material_import_is_all_or_nothing_and_keeps_lot_tracking_locked()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var used = await CreateMaterialAsync(client);
        await CreateDraftAsync(client, "stock-receipts", Receipt(site.Warehouse.Id, new ReceiptLineRequest(used.Id, null, null, null, null, 1)));
        var fresh = UniqueCode();

        var file = await FillTemplateAsync(client, MaterialTemplate,
            [fresh, "Hợp lệ", null, "KG", "NVL", null, null],
            [used.Code, "Đổi tên", null, "KG", "NVL", null, "Có"]);
        var result = await ReadAsync<ImportResult>(await UploadAsync(client, "/api/materials/import?updateExisting=true", file));

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors);
        Assert.Equal((3, MaterialImportColumns.TrackLots, ImportErrorCodes.TrackLotsLocked), (error.Row, error.Column, error.ErrorCode));
        Assert.Null(await FindMaterialAsync(client, fresh));
        Assert.Equal(used.Name, (await FindMaterialAsync(client, used.Code))!.Name);
    }

    [Fact]
    public async Task Opening_balance_import_creates_one_draft_per_warehouse()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var a = await CreateWarehouseAsync(client);
        var b = await CreateWarehouseAsync(client);
        var plain = await CreateMaterialAsync(client);
        var tracked = await CreateMaterialAsync(client, trackLots: true);

        var template = await client.GetByteArrayAsync(OpeningTemplate);
        using (var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(template)))
            Assert.Contains(workbook.Worksheet("Danh mục").Column(1).CellsUsed(), c => c.GetString() == plain.Code);

        var file = await FillTemplateAsync(client, OpeningTemplate,
            [plain.Code.ToLowerInvariant(), a.Warehouse.Name, "a-01", null, null, null, "10"],
            [tracked.Code, b.Warehouse.Code, null, "L-01", "01/01/2026", "31/12/2027", "5,5"],
            [tracked.Code, b.Warehouse.Code, "B-01", "L-01", "01/01/2026", "31/12/2027", "2"]);
        var date = Today.AddDays(-1);
        var result = await ReadAsync<OpeningBalanceImportResult>(await UploadAsync(client,
            $"/api/stock/opening-balance/import?documentDate={date:yyyy-MM-dd}", file));

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => $"{e.Row}/{e.Column}: {e.Message}")));
        Assert.Equal(2, result.Documents.Count);
        var forA = result.Documents.Single(d => d.WarehouseName == a.Warehouse.Name);
        var forB = result.Documents.Single(d => d.WarehouseName == b.Warehouse.Name);
        Assert.Equal((1, 2), (forA.LineCount, forB.LineCount));

        var draft = (await client.GetFromJsonAsync<StockDocumentDto>($"/api/stock-receipts/{forB.Id}", Json))!;
        Assert.Equal((StockDocumentStatus.Draft, StockDocumentReason.OpeningBalance, date),
            (draft.Status, draft.Reason, draft.DocumentDate));
        Assert.Equal(("L-01", (DateOnly?)new DateOnly(2027, 12, 31), 5.5m),
            (draft.Lines[0].LotNumber, draft.Lines[0].ExpiryDate, draft.Lines[0].Quantity));

        // Nothing is in stock until the drafts are approved.
        Assert.Equal(0, await QuantityAsync(client, plain.Id, a.Warehouse.Id, a.Shelf1.Id));
        await ApproveAsync(client, "stock-receipts", forA.Id);
        Assert.Equal(10, await QuantityAsync(client, plain.Id, a.Warehouse.Id, a.Shelf1.Id));
    }

    [Fact]
    public async Task Opening_balance_import_with_errors_creates_nothing()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var site = await CreateWarehouseAsync(client);
        var other = await CreateWarehouseAsync(client);
        var plain = await CreateMaterialAsync(client);
        var tracked = await CreateMaterialAsync(client, trackLots: true);

        var file = await FillTemplateAsync(client, OpeningTemplate,
            [plain.Code, site.Warehouse.Code, null, null, null, null, "3"],
            ["KHONG-CO", site.Warehouse.Code, null, null, null, null, "1"],
            [tracked.Code, site.Warehouse.Code, null, null, null, null, "1"],
            [plain.Code, other.Warehouse.Code, "Z-99", null, null, null, "1"]);
        var result = await ReadAsync<OpeningBalanceImportResult>(await UploadAsync(client, "/api/stock/opening-balance/import", file));

        Assert.False(result.Succeeded);
        Assert.Empty(result.Documents);
        Assert.Equal(
            [(3, ImportErrorCodes.UnknownMaterial), (4, ImportErrorCodes.LotRequired), (5, ImportErrorCodes.UnknownLocation)],
            result.Errors.Select(e => (e.Row, e.ErrorCode)));
        var drafts = (await client.GetFromJsonAsync<PagedResult<StockDocumentSummaryDto>>(
            $"/api/stock-receipts?warehouseId={site.Warehouse.Id}", Json))!;
        Assert.Equal(0, drafts.TotalCount);

        var future = await UploadAsync(client, $"/api/stock/opening-balance/import?documentDate={Today.AddDays(1):yyyy-MM-dd}",
            await FillTemplateAsync(client, OpeningTemplate, [plain.Code, site.Warehouse.Code, null, null, null, null, "3"]));
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }

    [Theory]
    [InlineData(Roles.HR)]
    [InlineData(Roles.Employee)]
    public async Task Imports_need_inventory_write_permissions(string role)
    {
        var client = await ClientWithRoleAsync(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(MaterialTemplate)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(OpeningTemplate)).StatusCode);
    }
}
