using System.Net;
using System.Net.Http.Json;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Employees;
using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using LvvErp.Infrastructure.Persistence;
using LvvErp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LvvErp.IntegrationTests.Employees;

public sealed class EmployeesTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Lookups_are_seeded()
    {
        var client = await ClientWithRoleAsync(Roles.Employee);

        var departments = await client.GetFromJsonAsync<List<LookupDto>>("/api/departments", Json);
        var positions = await client.GetFromJsonAsync<List<LookupDto>>("/api/positions", Json);

        Assert.Contains(departments!, d => d is { Code: "SX", Name: "Sản xuất" });
        Assert.Contains(positions!, p => p is { Code: "CN", Name: "Công nhân" });
    }

    [Fact]
    public async Task Employee_role_can_view_but_not_modify()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var existing = await CreateEmployeeAsync(hr, await NewEmployeeRequestAsync(hr));
        var client = await ClientWithRoleAsync(Roles.Employee);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/employees")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/employees/{existing.Id}")).StatusCode);

        var create = await client.PostAsJsonAsync("/api/employees", await NewEmployeeRequestAsync(client), Json);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/employees/{existing.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/employees/import/template")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousClient().GetAsync("/api/employees")).StatusCode);

    [Fact]
    public async Task HR_can_create_update_and_soft_delete()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var request = await NewEmployeeRequestAsync(hr, email: "test@lvv.vn");

        // Create
        var created = await CreateEmployeeAsync(hr, request with { Code = request.Code.ToLowerInvariant() });
        Assert.Equal(request.Code, created.Code); // normalized to upper-case
        Assert.Equal("Sản xuất", created.DepartmentName);
        Assert.Equal("Công nhân", created.PositionName);
        Assert.Equal(EmployeeStatus.Active, created.Status);

        // Update
        var kho = await LookupAsync(hr, "departments", "KHO");
        var update = await hr.PutAsJsonAsync($"/api/employees/{created.Id}",
            request with { FullName = "Tên Mới", DepartmentId = kho.Id, Status = EmployeeStatus.OnLeave }, Json);
        var updated = (await update.Content.ReadFromJsonAsync<EmployeeDto>(Json))!;
        Assert.Equal(("Tên Mới", "Kho", EmployeeStatus.OnLeave), (updated.FullName, updated.DepartmentName, updated.Status));
        Assert.NotNull(updated.UpdatedAt);

        // Delete (soft)
        Assert.Equal(HttpStatusCode.NoContent, (await hr.DeleteAsync($"/api/employees/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync($"/api/employees/{created.Id}")).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Employees.IgnoreQueryFilters().SingleAsync(e => e.Id == created.Id);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
        Assert.False(string.IsNullOrEmpty(row.DeletedBy));
        Assert.False(string.IsNullOrEmpty(row.CreatedBy));
    }

    [Fact]
    public async Task Duplicate_code_returns_409_but_code_is_reusable_after_delete()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var request = await NewEmployeeRequestAsync(hr);
        var first = await CreateEmployeeAsync(hr, request);

        var duplicate = await hr.PostAsJsonAsync("/api/employees", request with { Code = request.Code.ToLowerInvariant() }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        await hr.DeleteAsync($"/api/employees/{first.Id}");
        var reused = await hr.PostAsJsonAsync("/api/employees", request, Json);
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_field_errors()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var request = await NewEmployeeRequestAsync(hr);

        var badFields = await hr.PostAsJsonAsync("/api/employees",
            request with { Code = "has space", FullName = "", Email = "not-an-email" }, Json);
        var problem = await badFields.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest, badFields.StatusCode);
        Assert.Superset(new HashSet<string> { "Code", "FullName", "Email" }, problem!.Errors.Keys.ToHashSet());

        var badLookup = await hr.PostAsJsonAsync("/api/employees", request with { DepartmentId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, badLookup.StatusCode);
    }

    [Fact]
    public async Task Search_is_accent_and_case_insensitive_on_name_and_matches_code_and_email()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var token = Guid.NewGuid().ToString("N")[..8];
        var created = await CreateEmployeeAsync(hr,
            await NewEmployeeRequestAsync(hr, fullName: $"Nguyễn Thị Hằng {token}", email: $"hang.{token}@lvv.vn"));

        foreach (var search in new[] { $"nguyen thi hang {token}", $"NGUYỄN THỊ HẰNG {token}", created.Code.ToLowerInvariant(), $"hang.{token}@" })
        {
            var result = await hr.GetFromJsonAsync<PagedResult<EmployeeDto>>(
                $"/api/employees?search={Uri.EscapeDataString(search)}", Json);
            Assert.True(result!.Items.Any(e => e.Id == created.Id), $"search '{search}' should find the employee");
        }
    }

    [Fact]
    public async Task Names_sort_in_vietnamese_alphabetical_order()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var token = Guid.NewGuid().ToString("N")[..8];
        foreach (var name in new[] { "Ê", "Đặng", "Dương", "An", "Ơn" })
            await CreateEmployeeAsync(hr, await NewEmployeeRequestAsync(hr, fullName: $"{name} {token}"));

        var result = await hr.GetFromJsonAsync<PagedResult<EmployeeDto>>($"/api/employees?search={token}&sort=fullName", Json);

        // Vietnamese alphabet: ... D, Đ, E, Ê, ... O, Ô, Ơ (the default collation puts Đ, Ê, Ơ after Z).
        Assert.Equal(["An", "Dương", "Đặng", "Ê", "Ơn"], result!.Items.Select(e => e.FullName.Split(' ')[0]));
    }

    [Fact]
    public async Task List_supports_filters_paging_and_sorting()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var token = Guid.NewGuid().ToString("N")[..8];
        await CreateEmployeeAsync(hr, await NewEmployeeRequestAsync(hr, fullName: $"An {token}", department: "QC"));
        await CreateEmployeeAsync(hr, await NewEmployeeRequestAsync(hr, fullName: $"Bình {token}", department: "QC", status: EmployeeStatus.Resigned));
        await CreateEmployeeAsync(hr, await NewEmployeeRequestAsync(hr, fullName: $"Cường {token}", department: "KHO"));
        var qc = await LookupAsync(hr, "departments", "QC");

        var page1 = await hr.GetFromJsonAsync<PagedResult<EmployeeDto>>($"/api/employees?search={token}&pageSize=2&sort=-fullName", Json);
        Assert.Equal(3, page1!.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal([$"Cường {token}", $"Bình {token}"], page1.Items.Select(e => e.FullName));

        var page2 = await hr.GetFromJsonAsync<PagedResult<EmployeeDto>>($"/api/employees?search={token}&pageSize=2&page=2&sort=-fullName", Json);
        Assert.Equal($"An {token}", Assert.Single(page2!.Items).FullName);

        var qcOnly = await hr.GetFromJsonAsync<PagedResult<EmployeeDto>>($"/api/employees?search={token}&departmentId={qc.Id}", Json);
        Assert.Equal(2, qcOnly!.TotalCount);

        var resigned = await hr.GetFromJsonAsync<PagedResult<EmployeeDto>>($"/api/employees?search={token}&status=Resigned", Json);
        Assert.Equal($"Bình {token}", Assert.Single(resigned!.Items).FullName);
    }
}
