using System.Net.Http.Json;
using System.Text.Json;
using LvvErp.Application.Common.Time;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Application.Features.Inventory.Documents;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Infrastructure.Persistence;
using LvvErp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LvvErp.IntegrationTests.Inventory;

/// <summary>Helpers to set up warehouses, materials and stock through the API.</summary>
public abstract class InventoryTestBase(ApiFactory factory) : ApiTestBase(factory)
{
    protected static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, BusinessClock.Zone).DateTime);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    protected static async Task<JsonElement> ProblemAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    protected static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await ProblemAsync(response)).TryGetProperty("errorCode", out var code) ? code.GetString() : null;

    protected sealed record Site(WarehouseDto Warehouse, LocationDto Shelf1, LocationDto Shelf2);

    protected static async Task<Site> CreateWarehouseAsync(HttpClient client)
    {
        var warehouse = await ReadAsync<WarehouseDto>(await client.PostAsJsonAsync("/api/warehouses",
            new SaveWarehouseRequest(UniqueCode(), "Kho " + UniqueCode(), null)));
        var shelf1 = await ReadAsync<LocationDto>(await client.PostAsJsonAsync($"/api/warehouses/{warehouse.Id}/locations",
            new SaveLocationRequest("A-01", null)));
        var shelf2 = await ReadAsync<LocationDto>(await client.PostAsJsonAsync($"/api/warehouses/{warehouse.Id}/locations",
            new SaveLocationRequest("B-01", null)));
        return new Site(warehouse, shelf1, shelf2);
    }

    protected static async Task<MaterialDto> CreateMaterialAsync(HttpClient client, bool trackLots = false, string name = "Vật tư thử", decimal minStock = 0)
    {
        var units = await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/units", Json);
        var groups = await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/material-groups", Json);
        return await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials", new SaveMaterialRequest(
            UniqueCode(), name, null, units!.Single(u => u.Code == "KG").Id, groups!.Single(g => g.Code == "NVL").Id,
            minStock, trackLots), Json));
    }

    protected static SaveStockReceiptRequest Receipt(Guid warehouseId, params ReceiptLineRequest[] lines) =>
        new(Today, warehouseId, StockDocumentReason.Purchase, "Công ty TNHH Thép Việt", null, lines);

    protected static SaveStockIssueRequest Issue(Guid warehouseId, params IssueLineRequest[] lines) =>
        new(Today, warehouseId, StockDocumentReason.Production, null, null, null, lines);

    protected static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Json);

    protected static async Task<StockDocumentDto> CreateDraftAsync(HttpClient client, string resource, object body) =>
        await ReadAsync<StockDocumentDto>(await PostAsync(client, $"/api/{resource}", body));

    protected static async Task<StockDocumentDto> ApproveAsync(HttpClient client, string resource, Guid id) =>
        await ReadAsync<StockDocumentDto>(await client.PostAsync($"/api/{resource}/{id}/approve", null));

    protected static async Task<StockDocumentDto> ReceiveAsync(HttpClient client, Guid warehouseId, params ReceiptLineRequest[] lines)
    {
        var draft = await CreateDraftAsync(client, "stock-receipts", Receipt(warehouseId, lines));
        return await ApproveAsync(client, "stock-receipts", draft.Id);
    }

    protected static async Task<List<AvailableStockDto>> AvailableAsync(HttpClient client, Guid materialId, Guid warehouseId) =>
        (await client.GetFromJsonAsync<List<AvailableStockDto>>($"/api/stock/available?materialId={materialId}&warehouseId={warehouseId}", Json))!;

    protected static async Task<decimal> QuantityAsync(HttpClient client, Guid materialId, Guid warehouseId, Guid? locationId = null) =>
        (await AvailableAsync(client, materialId, warehouseId)).Where(a => a.LocationId == locationId).Sum(a => a.Quantity);

    /// <summary>Downloads a template from the API and fills data rows (row 2 onwards) with the given cell values.</summary>
    protected static async Task<byte[]> FillTemplateAsync(HttpClient client, string templateUrl, params string?[][] rows)
    {
        var template = await client.GetByteArrayAsync(templateUrl);
        using var workbook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(template));
        var sheet = workbook.Worksheet(1);
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                if (rows[r][c] is { } value) sheet.Cell(r + 2, c + 1).Value = value;
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    protected static Task<HttpResponseMessage> UploadAsync(HttpClient client, string url, byte[] file)
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return client.PostAsync(url, new MultipartFormDataContent { { content, "file", "data.xlsx" } });
    }

    protected async Task<List<StockTransaction>> LedgerAsync(Guid materialId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().StockTransactions.AsNoTracking()
            .Where(t => t.MaterialId == materialId).OrderBy(t => t.Id).ToListAsync();
    }
}
