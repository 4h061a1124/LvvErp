using System.Net.Http.Json;
using LvvErp.Application.Features.Identity;
using LvvErp.Application.Features.Purchasing.Orders;
using LvvErp.Application.Features.Purchasing.Requests;
using LvvErp.Application.Features.Purchasing.Suppliers;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Purchasing;
using LvvErp.IntegrationTests.Infrastructure;
using LvvErp.IntegrationTests.Inventory;

namespace LvvErp.IntegrationTests.Purchasing;

/// <summary>Users linked to employees of a department, suppliers, approved requests and orders.</summary>
public abstract class PurchasingTestBase(ApiFactory factory) : InventoryTestBase(factory)
{
    protected sealed record Actor(HttpClient Client, UserDto User);

    /// <summary>A new account linked to a new employee of the department (by code: SX, MH, KHO, BGD...).</summary>
    protected async Task<Actor> UserInAsync(string departmentCode, params string[] roles)
    {
        const string password = "Passw0rd!";
        var admin = await AdminClientAsync();
        var employee = await CreateEmployeeAsync(admin, await NewEmployeeRequestAsync(admin, department: departmentCode));
        var user = await ReadAsync<UserDto>(await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueName("u"), employee.FullName, null, password, roles, employee.Id), Json));
        return new Actor(await ClientForAsync(user.UserName, password), user);
    }

    protected Task<Actor> PurchaserAsync() => UserInAsync("MH", Roles.Purchaser);
    protected Task<Actor> PurchasingHeadAsync() => UserInAsync("MH", Roles.DepartmentHead);
    protected Task<Actor> DirectorAsync() => UserInAsync("BGD", Roles.Director);

    protected static async Task<SupplierDto> CreateSupplierAsync(HttpClient purchaser) =>
        await ReadAsync<SupplierDto>(await purchaser.PostAsJsonAsync("/api/suppliers",
            new SaveSupplierRequest(UniqueCode(), "NCC " + UniqueCode(), null, null, null, null, null, 30, null), Json));

    protected static SavePurchaseRequestRequest NewRequest(params (Guid MaterialId, decimal Quantity)[] lines) =>
        new(Today, Today.AddDays(7), "Bổ sung vật tư sản xuất",
            lines.Select(l => new PurchaseRequestLineRequest(l.MaterialId, l.Quantity)).ToList());

    protected static async Task<PurchaseRequestDto> PostRequestAsync(HttpClient client, string url, object? body = null) =>
        await ReadAsync<PurchaseRequestDto>(await client.PostAsJsonAsync(url, body, Json));

    /// <summary>Raised by a new SX employee, approved by a new SX head.</summary>
    protected async Task<PurchaseRequestDto> ApprovedRequestAsync(params (Guid MaterialId, decimal Quantity)[] lines)
    {
        var requester = await UserInAsync("SX", Roles.Employee);
        var head = await UserInAsync("SX", Roles.DepartmentHead);
        var draft = await PostRequestAsync(requester.Client, "/api/purchase-requests", NewRequest(lines));
        await PostRequestAsync(requester.Client, $"/api/purchase-requests/{draft.Id}/submit");
        return await PostRequestAsync(head.Client, $"/api/purchase-requests/{draft.Id}/approve");
    }

    protected static async Task<PurchaseRequestDto> GetRequestAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<PurchaseRequestDto>($"/api/purchase-requests/{id}", Json))!;

    protected static SavePurchaseOrderRequest NewOrder(Guid supplierId, Guid warehouseId, params PurchaseOrderLineRequest[] lines) =>
        new(supplierId, Today, Today.AddDays(5), warehouseId, 30, VatRate.Ten, null, lines);

    protected static PurchaseOrderLineRequest OrderLine(
        PurchaseRequestLineDto source, decimal? quantity = null, decimal unitPrice = 10_000, string? overOrderReason = null) =>
        new(source.MaterialId, quantity ?? source.Quantity, unitPrice, [source.Id], null, overOrderReason);

    protected static async Task<PurchaseOrderDto> PostOrderAsync(HttpClient client, string url, object? body = null) =>
        await ReadAsync<PurchaseOrderDto>(await client.PostAsJsonAsync(url, body, Json));

    protected static async Task<PurchaseOrderDto> GetOrderAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<PurchaseOrderDto>($"/api/purchase-orders/{id}", Json))!;

    protected static ReasonRequest Reason(string text = "Lý do thử") => new(text);
}
