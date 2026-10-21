using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Employees;
using LvvErp.Application.Features.Employees.Import;
using LvvErp.Domain.Features.Employees;
using LvvErp.Domain.Features.Identity;
using LvvErp.IntegrationTests.Infrastructure;

namespace LvvErp.IntegrationTests.Employees;

/// <summary>Exercises the real flow: download the template from the API, fill it, upload it.</summary>
public sealed class EmployeeImportTests(ApiFactory factory) : ApiTestBase(factory)
{
    private sealed record Row(string? Code, string? Name, string? Dept, string? Pos, string? Email = null, string? Phone = null, string? Status = null);

    private static async Task<byte[]> BuildFileAsync(HttpClient client, params Row[] rows)
    {
        var template = await client.GetByteArrayAsync("/api/employees/import/template");
        using var workbook = new XLWorkbook(new MemoryStream(template));
        var sheet = workbook.Worksheet(1);
        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            string?[] values = [r.Code, r.Name, r.Dept, r.Pos, r.Email, r.Phone, r.Status];
            for (var c = 0; c < values.Length; c++)
                if (values[c] is not null) sheet.Cell(i + 2, c + 1).Value = values[c];
        }
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] file, bool updateExisting = false)
    {
        var content = new ByteArrayContent(file);
        content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return await client.PostAsync($"/api/employees/import?updateExisting={updateExisting}",
            new MultipartFormDataContent { { content, "file", "nhan-vien.xlsx" } });
    }

    private static async Task<EmployeeImportResult> ImportAsync(HttpClient client, byte[] file, bool updateExisting = false)
    {
        var response = await UploadAsync(client, file, updateExisting);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EmployeeImportResult>(Json))!;
    }

    private static async Task<EmployeeDto?> FindByCodeAsync(HttpClient client, string code) =>
        (await client.GetFromJsonAsync<PagedResult<EmployeeDto>>($"/api/employees?search={code}", Json))!
            .Items.SingleOrDefault(e => e.Code == code);

    [Fact]
    public async Task Template_downloads_as_xlsx()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);

        var response = await hr.GetAsync("/api/employees/import/template");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        Assert.Contains(workbook.Worksheets, w => w.Name == "Danh mục");
    }

    [Fact]
    public async Task Valid_file_creates_employees_matching_lookups_by_code_or_name()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        string a = UniqueCode(), b = UniqueCode();
        var file = await BuildFileAsync(hr,
            new Row(a.ToLowerInvariant(), "Lê Văn Import", "sx", "Công nhân", "le@lvv.vn", "0901234567"),
            new Row(b, "Phạm Thị Import", "kho", "ky su", Status: "Tạm nghỉ"));

        var result = await ImportAsync(hr, file);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Equal((2, 2, 0), (result.TotalRows, result.Created, result.Updated));

        var first = await FindByCodeAsync(hr, a);
        Assert.Equal(("Sản xuất", "Công nhân", "0901234567", EmployeeStatus.Active),
            (first!.DepartmentName, first.PositionName, first.Phone, first.Status));
        var second = await FindByCodeAsync(hr, b);
        Assert.Equal(("Kho", "Kỹ sư", EmployeeStatus.OnLeave), (second!.DepartmentName, second.PositionName, second.Status));
    }

    [Fact]
    public async Task One_bad_row_means_nothing_is_saved()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        string good = UniqueCode(), bad = UniqueCode();
        var file = await BuildFileAsync(hr,
            new Row(good, "Dòng Hợp Lệ", "SX", "CN"),
            new Row(bad, "Dòng Lỗi", "Phòng Không Tồn Tại", "CN", Email: "sai-email"));

        var result = await ImportAsync(hr, file);

        Assert.False(result.Succeeded);
        Assert.Equal((0, 0), (result.Created, result.Updated));
        Assert.Contains(result.Errors, e => e is { Row: 3, Column: ImportColumns.Department, ErrorCode: ImportErrorCodes.UnknownDepartment });
        Assert.Contains(result.Errors, e => e is { Row: 3, Column: ImportColumns.Email });
        Assert.DoesNotContain(result.Errors, e => e.Row == 2);
        Assert.Null(await FindByCodeAsync(hr, good));
    }

    [Fact]
    public async Task Existing_code_is_rejected_by_default_and_updated_when_option_is_on()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);
        var existing = await CreateEmployeeAsync(hr,
            await NewEmployeeRequestAsync(hr, fullName: "Tên Cũ", email: "giu.nguyen@lvv.vn"));
        var newCode = UniqueCode();
        var file = await BuildFileAsync(hr,
            new Row(existing.Code, "Tên Mới", "QC", "KS", Email: null, Status: "Đã nghỉ"),
            new Row(newCode, "Người Mới", "SX", "CN"));

        // Default: duplicate is an error, nothing saved.
        var rejected = await ImportAsync(hr, file);
        Assert.False(rejected.Succeeded);
        var error = Assert.Single(rejected.Errors);
        Assert.Equal((2, ImportErrorCodes.AlreadyExists), (error.Row, error.ErrorCode));
        Assert.Null(await FindByCodeAsync(hr, newCode));

        // Option on: existing row updated, new row created.
        var result = await ImportAsync(hr, file, updateExisting: true);
        Assert.True(result.Succeeded);
        Assert.Equal((1, 1), (result.Created, result.Updated));

        var updated = await hr.GetFromJsonAsync<EmployeeDto>($"/api/employees/{existing.Id}", Json);
        Assert.Equal(("Tên Mới", "Quản lý chất lượng", "Kỹ sư", EmployeeStatus.Resigned),
            (updated!.FullName, updated.DepartmentName, updated.PositionName, updated.Status));
        Assert.Equal("giu.nguyen@lvv.vn", updated.Email); // blank cell keeps the current value
        Assert.NotNull(await FindByCodeAsync(hr, newCode));
    }

    [Fact]
    public async Task Invalid_files_return_400()
    {
        var hr = await ClientWithRoleAsync(Roles.HR);

        var notExcel = await UploadAsync(hr, "ma,ten\n1,a"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, notExcel.StatusCode);

        var empty = await UploadAsync(hr, await BuildFileAsync(hr));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Sheet1").Cell(1, 1).Value = "Something else";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var wrongHeader = await UploadAsync(hr, stream.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, wrongHeader.StatusCode);
    }
}
