using LvvErp.Application.Common.Import;
using LvvErp.Application.Common.Models;
using LvvErp.Application.Features.Employees.Import;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.UnitTests.Employees;

public sealed class EmployeeImportValidatorTests
{
    private static readonly LookupDto Production = new(Guid.NewGuid(), "SX", "Sản xuất");
    private static readonly LookupDto Warehouse = new(Guid.NewGuid(), "KHO", "Kho");
    private static readonly LookupDto Worker = new(Guid.NewGuid(), "CN", "Công nhân");
    private static readonly LookupDto Engineer = new(Guid.NewGuid(), "KS", "Kỹ sư");

    private static EmployeeImportValidator Create(bool updateExisting = false, params string[] existingCodes) =>
        new([Production, Warehouse], [Worker, Engineer], existingCodes.ToHashSet(), updateExisting);

    private static EmployeeImportRow Row(
        int row = 2, string? code = "NV001", string? name = "Nguyễn Văn A", string? dept = "SX", string? pos = "CN",
        string? email = null, string? phone = null, string? status = null) =>
        new(row, code, name, dept, pos, email, phone, status);

    [Fact]
    public void Valid_row_is_resolved_and_code_upper_cased()
    {
        var (rows, errors) = Create().Validate([Row(code: "nv001", email: "a@lvv.vn", phone: "0901 234 567")]);

        Assert.Empty(errors);
        var row = Assert.Single(rows);
        Assert.Equal("NV001", row.Code);
        Assert.Equal(Production.Id, row.DepartmentId);
        Assert.Equal(Worker.Id, row.PositionId);
        Assert.Null(row.Status);
        Assert.False(row.IsUpdate);
    }

    [Theory]
    [InlineData("SX")]
    [InlineData("sx")]
    [InlineData("Sản xuất")]
    [InlineData("san xuat")]
    [InlineData("  SAN XUAT ")]
    public void Department_matches_by_code_or_name_ignoring_case_and_accents(string dept)
    {
        var (rows, errors) = Create().Validate([Row(dept: dept)]);

        Assert.Empty(errors);
        Assert.Equal(Production.Id, rows[0].DepartmentId);
    }

    [Fact]
    public void Position_matches_by_name_without_accents()
    {
        var (rows, _) = Create().Validate([Row(pos: "ky su")]);
        Assert.Equal(Engineer.Id, rows[0].PositionId);
    }

    [Theory]
    [InlineData("Đang làm", EmployeeStatus.Active)]
    [InlineData("dang lam", EmployeeStatus.Active)]
    [InlineData("Active", EmployeeStatus.Active)]
    [InlineData("Tạm nghỉ", EmployeeStatus.OnLeave)]
    [InlineData("OnLeave", EmployeeStatus.OnLeave)]
    [InlineData("Đã nghỉ", EmployeeStatus.Resigned)]
    [InlineData("nghi viec", EmployeeStatus.Resigned)]
    public void Status_accepts_vietnamese_and_english_labels(string status, EmployeeStatus expected)
    {
        var (rows, errors) = Create().Validate([Row(status: status)]);

        Assert.Empty(errors);
        Assert.Equal(expected, rows[0].Status);
    }

    [Fact]
    public void Missing_required_fields_are_reported_per_column()
    {
        var (rows, errors) = Create().Validate([Row(code: null, name: " ", dept: null, pos: null)]);

        Assert.Empty(rows);
        Assert.Equal(
            [EmployeeImportColumns.Code, EmployeeImportColumns.FullName, EmployeeImportColumns.Department, EmployeeImportColumns.Position],
            errors.Select(e => e.Column));
        Assert.All(errors, e => Assert.Equal(ImportErrorCodes.Required, e.ErrorCode));
        Assert.All(errors, e => Assert.Equal(2, e.Row));
    }

    [Fact]
    public void Unknown_lookups_and_bad_optional_values_are_reported_with_value()
    {
        var (_, errors) = Create().Validate([Row(dept: "Marketing", pos: "CEO", email: "not-email", phone: "abc", status: "Đi chơi")]);

        Assert.Contains(errors, e => e is { ErrorCode: ImportErrorCodes.UnknownDepartment, Value: "Marketing" });
        Assert.Contains(errors, e => e is { ErrorCode: ImportErrorCodes.UnknownPosition, Value: "CEO" });
        Assert.Contains(errors, e => e is { Column: EmployeeImportColumns.Email, ErrorCode: ImportErrorCodes.InvalidFormat });
        Assert.Contains(errors, e => e is { Column: EmployeeImportColumns.Phone, ErrorCode: ImportErrorCodes.InvalidFormat });
        Assert.Contains(errors, e => e is { ErrorCode: ImportErrorCodes.InvalidStatus, Value: "Đi chơi" });
    }

    [Fact]
    public void Code_too_long_or_with_spaces_is_rejected()
    {
        var (_, errors) = Create().Validate([
            Row(row: 2, code: new string('A', Employee.CodeMaxLength + 1)),
            Row(row: 3, code: "NV 01"),
        ]);

        Assert.Contains(errors, e => e is { Row: 2, ErrorCode: ImportErrorCodes.TooLong });
        Assert.Contains(errors, e => e is { Row: 3, ErrorCode: ImportErrorCodes.InvalidFormat });
    }

    [Fact]
    public void Duplicate_code_in_file_points_to_first_row()
    {
        var (rows, errors) = Create().Validate([Row(row: 2, code: "NV001"), Row(row: 5, code: "nv001")]);

        var error = Assert.Single(errors);
        Assert.Equal(5, error.Row);
        Assert.Equal(ImportErrorCodes.DuplicateInFile, error.ErrorCode);
        Assert.Contains("row 2", error.Message);
        Assert.Single(rows);
    }

    [Fact]
    public void Existing_code_is_an_error_when_update_is_off()
    {
        var (rows, errors) = Create(updateExisting: false, "NV001").Validate([Row()]);

        Assert.Empty(rows);
        Assert.Equal(ImportErrorCodes.AlreadyExists, Assert.Single(errors).ErrorCode);
    }

    [Fact]
    public void Existing_code_becomes_an_update_when_update_is_on()
    {
        var (rows, errors) = Create(updateExisting: true, "NV001").Validate([Row(), Row(row: 3, code: "NV002")]);

        Assert.Empty(errors);
        Assert.True(rows.Single(r => r.Code == "NV001").IsUpdate);
        Assert.False(rows.Single(r => r.Code == "NV002").IsUpdate);
    }
}
