using LvvErp.Application.Common.Import;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.Application.Features.Employees.Import;

/// <summary>Raw cell values of one data row, as typed by the user.</summary>
public sealed record EmployeeImportRow(
    int RowNumber,
    string? Code,
    string? FullName,
    string? Department,
    string? Position,
    string? Email,
    string? Phone,
    string? Status)
{
    public static EmployeeImportRow From(ExcelRow row) => new(
        row.RowNumber,
        row[EmployeeImportColumns.Code],
        row[EmployeeImportColumns.FullName],
        row[EmployeeImportColumns.Department],
        row[EmployeeImportColumns.Position],
        row[EmployeeImportColumns.Email],
        row[EmployeeImportColumns.Phone],
        row[EmployeeImportColumns.Status]);
}

/// <summary>Column keys, also used in <see cref="ImportError.Column"/>.</summary>
public static class EmployeeImportColumns
{
    public const string Code = "Code";
    public const string FullName = "FullName";
    public const string Department = "Department";
    public const string Position = "Position";
    public const string Email = "Email";
    public const string Phone = "Phone";
    public const string Status = "Status";

    // Order = column order in the file. Headers are Vietnamese (end users); "*" = required.
    public static readonly IReadOnlyList<ExcelColumn> All =
    [
        new(Code, "Mã NV *", 14, IsText: true),
        new(FullName, "Họ tên *", 30),
        new(Department, "Bộ phận *", 24, ListName: Department),
        new(Position, "Chức danh *", 22, ListName: Position),
        new(Email, "Email", 28),
        new(Phone, "Số điện thoại", 16, IsText: true),
        new(Status, "Trạng thái", 14, ListName: Status),
    ];
}

/// <summary>A row that passed validation, with lookups resolved.</summary>
public sealed record ValidatedImportRow(
    int RowNumber,
    string Code,
    string FullName,
    Guid DepartmentId,
    Guid PositionId,
    string? Email,
    string? Phone,
    EmployeeStatus? Status,
    bool IsUpdate);
