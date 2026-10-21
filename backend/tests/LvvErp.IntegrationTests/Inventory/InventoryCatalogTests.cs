using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Inventory.Catalog;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Inventory;
using LvvErp.Infrastructure.Persistence;
using LvvErp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LvvErp.IntegrationTests.Inventory;

public sealed class InventoryCatalogTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<CatalogItemDto> CreateUnitAsync(HttpClient client, string? code = null) =>
        await ReadAsync<CatalogItemDto>(await client.PostAsJsonAsync("/api/units",
            new SaveCatalogItemRequest(code ?? UniqueCode(), "Đơn vị " + UniqueCode())));

    private static async Task<SaveMaterialRequest> NewMaterialRequestAsync(HttpClient client, Guid? unitId = null)
    {
        var units = await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/units", Json);
        var groups = await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/material-groups", Json);
        return new SaveMaterialRequest(UniqueCode(), "Bu lông lục giác", "M8x30, inox 304",
            unitId ?? units!.Single(u => u.Code == "CAI").Id, groups!.Single(g => g.Code == "PT").Id, 100, TrackLots: false);
    }

    [Fact]
    public async Task Units_and_groups_are_seeded()
    {
        var client = await ClientWithRoleAsync(Roles.Employee);

        var units = await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/units", Json);
        var groups = await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/material-groups", Json);

        Assert.Contains(units!, u => u is { Code: "KG", Name: "Kg" });
        Assert.Contains(groups!, g => g is { Code: "NVL", Name: "Nguyên vật liệu" });
    }

    [Theory]
    [InlineData(Roles.HR)]
    [InlineData(Roles.Employee)]
    public async Task Other_roles_can_view_but_not_change_the_catalog(string role)
    {
        var client = await ClientWithRoleAsync(role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/units")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/materials")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/warehouses")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/units", new SaveCatalogItemRequest(UniqueCode(), UniqueCode()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/warehouses", new SaveWarehouseRequest(UniqueCode(), UniqueCode(), null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/materials", await NewMaterialRequestAsync(client), Json)).StatusCode);
    }

    [Fact]
    public async Task Storekeeper_cannot_manage_employees_or_users()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/employees/import/template")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Storekeeper_manages_units_with_unique_code_and_name()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var code = UniqueCode();

        var created = await ReadAsync<CatalogItemDto>(
            await client.PostAsJsonAsync("/api/units", new SaveCatalogItemRequest(code.ToLowerInvariant(), "Đơn vị " + code)));
        Assert.Equal(code, created.Code);

        var dupCode = await client.PostAsJsonAsync("/api/units", new SaveCatalogItemRequest(code, "Khác " + code));
        Assert.Equal(HttpStatusCode.Conflict, dupCode.StatusCode);
        Assert.Equal("DuplicateCode", await ErrorCodeAsync(dupCode));

        var dupName = await client.PostAsJsonAsync("/api/units", new SaveCatalogItemRequest(UniqueCode(), "Đơn vị " + code));
        Assert.Equal("DuplicateName", await ErrorCodeAsync(dupName));

        // Deactivated entries disappear from pickers but stay in the full list.
        await ReadAsync<CatalogItemDto>(await client.PutAsJsonAsync($"/api/units/{created.Id}",
            new SaveCatalogItemRequest(code, "Đơn vị " + code, IsActive: false)));
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/units?activeOnly=true", Json))!,
            u => u.Id == created.Id);
        Assert.Contains((await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/units", Json))!, u => u.Id == created.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/units/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Unit_used_by_a_material_cannot_be_deleted()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var unit = await CreateUnitAsync(client);
        await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials",
            await NewMaterialRequestAsync(client, unit.Id), Json));

        var response = await client.DeleteAsync($"/api/units/{unit.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("InUse", await ErrorCodeAsync(response));
        Assert.Contains((await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/units", Json))!, u => u.Id == unit.Id);
    }

    [Fact]
    public async Task Warehouses_have_locations_unique_per_warehouse()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var main = await ReadAsync<WarehouseDto>(await client.PostAsJsonAsync("/api/warehouses",
            new SaveWarehouseRequest(UniqueCode(), "Kho " + UniqueCode(), "Nhà xưởng A")));
        var spare = await ReadAsync<WarehouseDto>(await client.PostAsJsonAsync("/api/warehouses",
            new SaveWarehouseRequest(UniqueCode(), "Kho " + UniqueCode(), null)));

        var location = await ReadAsync<LocationDto>(await client.PostAsJsonAsync($"/api/warehouses/{main.Id}/locations",
            new SaveLocationRequest("a-01-03", "Kệ A, tầng 1, ô 3")));
        Assert.Equal("A-01-03", location.Code);

        var dup = await client.PostAsJsonAsync($"/api/warehouses/{main.Id}/locations", new SaveLocationRequest("A-01-03", null));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var otherWarehouse = await client.PostAsJsonAsync($"/api/warehouses/{spare.Id}/locations", new SaveLocationRequest("A-01-03", null));
        Assert.Equal(HttpStatusCode.Created, otherWarehouse.StatusCode);

        var reloaded = await client.GetFromJsonAsync<WarehouseDto>($"/api/warehouses/{main.Id}", Json);
        Assert.Equal(1, reloaded!.LocationCount);

        // An unused warehouse can be deleted together with its locations.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/warehouses/{main.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PutAsJsonAsync($"/api/locations/{location.Id}", new SaveLocationRequest("A-01-03", null))).StatusCode);
    }

    [Fact]
    public async Task Material_crud_with_validation()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var request = await NewMaterialRequestAsync(client);

        var created = await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials",
            request with { Code = request.Code.ToLowerInvariant(), MinStock = 12.5m, TrackLots = true }, Json));
        Assert.Equal((request.Code, "Cái", "Phụ tùng thay thế", 12.5m, true),
            (created.Code, created.UnitName, created.GroupName, created.MinStock, created.TrackLots));

        var dup = await client.PostAsJsonAsync("/api/materials", request, Json);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/materials", request with { Code = UniqueCode(), UnitId = Guid.NewGuid() }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/materials", request with { Code = UniqueCode(), MinStock = -1 }, Json)).StatusCode);

        var updated = await ReadAsync<MaterialDto>(await client.PutAsJsonAsync($"/api/materials/{created.Id}",
            request with { Name = "Bu lông đầu tròn", Specification = null, IsActive = false }, Json));
        Assert.Equal(("Bu lông đầu tròn", null, false), (updated.Name, updated.Specification, updated.IsActive));
        Assert.NotNull(updated.UpdatedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/materials/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/materials/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Inactive_unit_cannot_be_picked_for_new_material_but_existing_link_stays()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var unit = await CreateUnitAsync(client);
        var request = await NewMaterialRequestAsync(client, unit.Id);
        var material = await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials", request, Json));

        await ReadAsync<CatalogItemDto>(await client.PutAsJsonAsync($"/api/units/{unit.Id}",
            new SaveCatalogItemRequest(unit.Code, unit.Name, IsActive: false)));

        var keep = await client.PutAsJsonAsync($"/api/materials/{material.Id}", request with { Name = "Đổi tên" }, Json);
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        var fresh = await client.PostAsJsonAsync("/api/materials", request with { Code = UniqueCode() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, fresh.StatusCode);
    }

    [Fact]
    public async Task Material_list_searches_without_accents_and_filters()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var token = Guid.NewGuid().ToString("N")[..8];
        var groups = (await client.GetFromJsonAsync<List<CatalogItemDto>>("/api/material-groups", Json))!;
        var request = await NewMaterialRequestAsync(client);

        var paint = await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials",
            request with { Code = UniqueCode(), Name = $"Sơn chống gỉ {token}", Specification = "Màu đỏ, thùng 18 lít",
                GroupId = groups.Single(g => g.Code == "VTTH").Id }, Json));
        var oldPaint = await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials",
            request with { Code = UniqueCode(), Name = $"Sơn lót {token}", IsActive = false }, Json));

        var byName = await client.GetFromJsonAsync<PagedResult<MaterialDto>>($"/api/materials?search=son chong gi {token}", Json);
        Assert.Equal(paint.Id, Assert.Single(byName!.Items).Id);

        var bothByToken = await client.GetFromJsonAsync<PagedResult<MaterialDto>>($"/api/materials?search={token}", Json);
        Assert.Equal(2, bothByToken!.TotalCount);

        var active = await client.GetFromJsonAsync<PagedResult<MaterialDto>>($"/api/materials?search={token}&isActive=true", Json);
        Assert.Equal(paint.Id, Assert.Single(active!.Items).Id);

        var inGroup = await client.GetFromJsonAsync<PagedResult<MaterialDto>>(
            $"/api/materials?search={token}&groupId={groups.Single(g => g.Code == "PT").Id}", Json);
        Assert.Equal(oldPaint.Id, Assert.Single(inGroup!.Items).Id);

        var spec = await client.GetFromJsonAsync<PagedResult<MaterialDto>>(
            $"/api/materials?search={Uri.EscapeDataString("thung 18 lit")}", Json);
        Assert.Contains(spec!.Items, m => m.Id == paint.Id);
    }

    [Fact]
    public async Task Material_photo_upload()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var material = await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials",
            await NewMaterialRequestAsync(client), Json));

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var upload = await client.PostAsync($"/api/materials/{material.Id}/photo",
            new MultipartFormDataContent { { file, "file", "a.png" } });

        var withPhoto = await ReadAsync<MaterialDto>(upload);
        Assert.StartsWith("/files/materials/", withPhoto.PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, (await AnonymousClient().GetAsync(withPhoto.PhotoUrl)).StatusCode);
    }

    [Fact]
    public async Task Material_with_lots_cannot_be_deleted()
    {
        var client = await ClientWithRoleAsync(Roles.Storekeeper);
        var material = await ReadAsync<MaterialDto>(await client.PostAsJsonAsync("/api/materials",
            await NewMaterialRequestAsync(client) with { TrackLots = true }, Json));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Lots.Add(new Lot { MaterialId = material.Id, LotNumber = "L2026-001" });
            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/materials/{material.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("InUse", await ErrorCodeAsync(response));

        await using var check = Factory.Services.CreateAsyncScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<AppDbContext>().Materials.AnyAsync(m => m.Id == material.Id));
    }
}
