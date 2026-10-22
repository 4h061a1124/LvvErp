using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Common.Security;
using LvvErp.Application.Features.Identity;
using LvvErp.Application.Features.Purchasing.Settings;
using LvvErp.Application.Features.Purchasing.Suppliers;
using LvvErp.Domain.Features.Identity;
using LvvErp.Domain.Features.Purchasing;
using LvvErp.IntegrationTests.Infrastructure;
using LvvErp.IntegrationTests.Inventory;

namespace LvvErp.IntegrationTests.Purchasing;

public sealed class PurchasingFoundationTests(ApiFactory factory) : InventoryTestBase(factory)
{
    private static string UniqueTaxCode() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();

    private static SaveSupplierRequest NewSupplier(string? code = null, string? taxCode = null) =>
        new(code ?? UniqueCode(), "Công ty TNHH Thép Việt", taxCode ?? UniqueTaxCode(), "Hà Nội", "Anh Hùng",
            "0912345678", "sales@thepviet.vn", 30, "Chuyển khoản");

    [Fact]
    public async Task Purchaser_manages_suppliers()
    {
        var client = await ClientWithRoleAsync(Roles.Purchaser);
        var request = NewSupplier(code: UniqueCode().ToLowerInvariant());

        var created = await ReadAsync<SupplierDto>(await client.PostAsJsonAsync("/api/suppliers", request, Json));
        Assert.Equal(request.Code.ToUpperInvariant(), created.Code);
        Assert.True(created.IsActive);

        var updated = await ReadAsync<SupplierDto>(await client.PutAsJsonAsync($"/api/suppliers/{created.Id}",
            request with { Name = "Thép Việt (chi nhánh)", TaxCode = null, PaymentTermDays = 45, IsActive = false }, Json));
        Assert.Equal(("Thép Việt (chi nhánh)", null, 45, false),
            (updated.Name, updated.TaxCode, updated.PaymentTermDays, updated.IsActive));

        var activeOnly = await client.GetFromJsonAsync<PagedResult<SupplierDto>>($"/api/suppliers?search={created.Code}&isActive=true", Json);
        Assert.Empty(activeOnly!.Items);
        var byName = await client.GetFromJsonAsync<PagedResult<SupplierDto>>("/api/suppliers?search=thep viet (chi nhanh)&pageSize=100", Json);
        Assert.Contains(byName!.Items, s => s.Id == created.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/suppliers/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/suppliers/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_code_or_tax_code_returns_409_with_error_code()
    {
        var client = await ClientWithRoleAsync(Roles.Purchaser);
        var first = await ReadAsync<SupplierDto>(await client.PostAsJsonAsync("/api/suppliers", NewSupplier(), Json));

        var sameCode = await client.PostAsJsonAsync("/api/suppliers", NewSupplier(code: first.Code.ToLowerInvariant()), Json);
        Assert.Equal(HttpStatusCode.Conflict, sameCode.StatusCode);
        Assert.Equal(ErrorCodes.DuplicateCode, await ErrorCodeAsync(sameCode));

        var sameTax = await client.PostAsJsonAsync("/api/suppliers", NewSupplier(taxCode: first.TaxCode), Json);
        Assert.Equal(HttpStatusCode.Conflict, sameTax.StatusCode);
        Assert.Equal(ErrorCodes.DuplicateTaxCode, await ErrorCodeAsync(sameTax));

        // Re-saving a supplier with its own tax code is fine.
        var resave = await client.PutAsJsonAsync($"/api/suppliers/{first.Id}", NewSupplier(first.Code, first.TaxCode), Json);
        Assert.Equal(HttpStatusCode.OK, resave.StatusCode);
    }

    [Fact]
    public async Task Invalid_supplier_returns_400()
    {
        var client = await ClientWithRoleAsync(Roles.Purchaser);

        var response = await client.PostAsJsonAsync("/api/suppliers", NewSupplier(taxCode: "12345") with { PaymentTermDays = 400 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ProblemAsync(response)).GetProperty("errors");
        Assert.True(errors.TryGetProperty("TaxCode", out _));
        Assert.True(errors.TryGetProperty("PaymentTermDays", out _));
    }

    [Fact]
    public async Task Storekeeper_can_view_suppliers_but_not_change_them_and_employee_cannot_view()
    {
        var storekeeper = await ClientWithRoleAsync(Roles.Storekeeper);
        Assert.Equal(HttpStatusCode.OK, (await storekeeper.GetAsync("/api/suppliers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.PostAsJsonAsync("/api/suppliers", NewSupplier(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await storekeeper.GetAsync("/api/suppliers/import/template")).StatusCode);

        var employee = await ClientWithRoleAsync(Roles.Employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/suppliers")).StatusCode);
    }

    [Fact]
    public async Task Supplier_import_creates_then_updates_and_rejects_foreign_tax_code()
    {
        var client = await ClientWithRoleAsync(Roles.Purchaser);
        var taken = await ReadAsync<SupplierDto>(await client.PostAsJsonAsync("/api/suppliers", NewSupplier(), Json));
        string code = UniqueCode(), tax = UniqueTaxCode();
        const string template = "/api/suppliers/import/template";

        var file = await FillTemplateAsync(client, template,
            [code.ToLowerInvariant(), "Công ty Bao bì Minh Phát", tax, "Bình Dương", null, null, null, "15", null]);
        var result = await ReadAsync<ImportResult>(await UploadAsync(client, "/api/suppliers/import", file));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => $"{e.Row}/{e.Column}: {e.Message}")));
        Assert.Equal((1, 0), (result.Created, result.Updated));

        // Blank optional cells keep their value on update; a tax code of another supplier fails the whole file.
        var update = await FillTemplateAsync(client, template,
            [code, "Bao bì Minh Phát", null, null, "Chị Lan", null, null, null, null],
            [UniqueCode(), "NCC khác", taken.TaxCode, null, null, null, null, null, null]);
        var rejected = await ReadAsync<ImportResult>(await UploadAsync(client, "/api/suppliers/import?updateExisting=true", update));
        Assert.False(rejected.Succeeded);
        Assert.Equal((3, ImportErrorCodes.DuplicateTaxCode), (Assert.Single(rejected.Errors).Row, rejected.Errors[0].ErrorCode));

        var fixedFile = await FillTemplateAsync(client, template, [code, "Bao bì Minh Phát", null, null, "Chị Lan", null, null, null, null]);
        var updated = await ReadAsync<ImportResult>(await UploadAsync(client, "/api/suppliers/import?updateExisting=true", fixedFile));
        Assert.Equal((0, 1), (updated.Created, updated.Updated));

        var supplier = (await client.GetFromJsonAsync<PagedResult<SupplierDto>>($"/api/suppliers?search={code}", Json))!.Items.Single();
        Assert.Equal(("Bao bì Minh Phát", tax, "Bình Dương", "Chị Lan", 15),
            (supplier.Name, supplier.TaxCode, supplier.Address, supplier.ContactName, supplier.PaymentTermDays));
    }

    [Fact]
    public async Task Settings_are_seeded_and_only_admin_changes_them()
    {
        var storekeeper = await ClientWithRoleAsync(Roles.Storekeeper);
        var settings = (await storekeeper.GetFromJsonAsync<PurchasingSettingsDto>("/api/purchasing/settings", Json))!;
        Assert.Equal(PurchasingSettings.DefaultDirectorApprovalThreshold, settings.DirectorApprovalThreshold);
        Assert.Equal("Mua hàng", settings.PurchasingDepartmentName);

        var employee = await ClientWithRoleAsync(Roles.Employee);
        Assert.Equal(HttpStatusCode.Forbidden, (await employee.GetAsync("/api/purchasing/settings")).StatusCode);

        var request = new SavePurchasingSettingsRequest(settings.DirectorApprovalThreshold, settings.PurchasingDepartmentId);
        var purchaser = await ClientWithRoleAsync(Roles.Purchaser);
        Assert.Equal(HttpStatusCode.Forbidden, (await purchaser.PutAsJsonAsync("/api/purchasing/settings", request, Json)).StatusCode);

        var admin = await AdminClientAsync();
        var unknownDepartment = await admin.PutAsJsonAsync("/api/purchasing/settings", request with { PurchasingDepartmentId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, unknownDepartment.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync("/api/purchasing/settings", request with { DirectorApprovalThreshold = 0 }, Json)).StatusCode);

        var saved = await ReadAsync<PurchasingSettingsDto>(await admin.PutAsJsonAsync("/api/purchasing/settings",
            request with { DirectorApprovalThreshold = 80_000_000, CompanyName = " Công ty CP Cơ khí LVV ", CompanyTaxCode = "0109876543" }, Json));
        Assert.Equal((80_000_000m, ApiFactory.AdminUserName), (saved.DirectorApprovalThreshold, saved.UpdatedBy));
        Assert.Equal(("Công ty CP Cơ khí LVV", "0109876543", (string?)null), (saved.CompanyName, saved.CompanyTaxCode, saved.CompanyAddress));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PutAsJsonAsync("/api/purchasing/settings", request with { CompanyTaxCode = "123" }, Json)).StatusCode);

        // Restore, so other tests keep the defaults.
        await ReadAsync<PurchasingSettingsDto>(await admin.PutAsJsonAsync("/api/purchasing/settings", request, Json));
    }

    [Fact]
    public async Task Linked_employee_gives_me_and_user_list_the_department()
    {
        var admin = await AdminClientAsync();
        var employee = await CreateEmployeeAsync(admin, await NewEmployeeRequestAsync(admin, fullName: "Trần Thị Mua", department: "MH"));
        const string password = "Passw0rd!";
        var user = await ReadAsync<UserDto>(await admin.PostAsJsonAsync("/api/users",
            new CreateUserRequest(UniqueName("mh"), "Trần Thị Mua", null, password, [Roles.Purchaser], employee.Id), Json));

        Assert.Equal((employee.Code, "Trần Thị Mua", "Mua hàng"), (user.EmployeeCode, user.EmployeeName, user.DepartmentName));
        var listed = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users", Json))!.Single(u => u.Id == user.Id);
        Assert.Equal("Mua hàng", listed.DepartmentName);

        var me = (await (await ClientForAsync(user.UserName, password)).GetFromJsonAsync<CurrentUserDto>("/api/auth/me", Json))!;
        Assert.Equal((employee.Id, employee.DepartmentId, "Mua hàng"), (me.EmployeeId, me.DepartmentId, me.DepartmentName));
        Assert.Contains(Permissions.PurchasingManageOrders, me.Permissions);
        Assert.DoesNotContain(Permissions.PurchasingApproveOrders, me.Permissions);
    }
}
