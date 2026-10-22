namespace LvvErp.Application.Features.Identity;

public sealed record UserDto(
    Guid Id,
    string UserName,
    string FullName,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    Guid? EmployeeId,
    string? EmployeeCode,
    string? EmployeeName,
    string? DepartmentName);

public sealed record CreateUserRequest(
    string UserName,
    string FullName,
    string? Email,
    string Password,
    IReadOnlyList<string> Roles,
    Guid? EmployeeId = null);

/// <summary>Leave <see cref="NewPassword"/> null to keep the current password.</summary>
public sealed record UpdateUserRequest(
    string FullName,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsActive,
    string? NewPassword,
    Guid? EmployeeId = null);
