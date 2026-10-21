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
    string? Status);

/// <summary>
/// One problem in the file. <see cref="Column"/> is a field key (Code, FullName, ...) and
/// <see cref="ErrorCode"/> a stable code, so the frontend can show a translated message;
/// <see cref="Message"/> is an English fallback.
/// </summary>
public sealed record EmployeeImportError(int Row, string Column, string ErrorCode, string Message, string? Value = null);

public sealed record EmployeeImportResult(
    bool Succeeded,
    int TotalRows,
    int Created,
    int Updated,
    IReadOnlyList<EmployeeImportError> Errors);

public static class ImportColumns
{
    public const string Code = "Code";
    public const string FullName = "FullName";
    public const string Department = "Department";
    public const string Position = "Position";
    public const string Email = "Email";
    public const string Phone = "Phone";
    public const string Status = "Status";
}

public static class ImportErrorCodes
{
    public const string Required = "Required";
    public const string TooLong = "TooLong";
    public const string InvalidFormat = "InvalidFormat";
    public const string UnknownDepartment = "UnknownDepartment";
    public const string UnknownPosition = "UnknownPosition";
    public const string InvalidStatus = "InvalidStatus";
    public const string DuplicateInFile = "DuplicateInFile";
    public const string AlreadyExists = "AlreadyExists";
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
