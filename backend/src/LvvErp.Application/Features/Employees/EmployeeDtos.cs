using LvvErp.Domain.Features.Employees;

namespace LvvErp.Application.Features.Employees;

public sealed record EmployeeDto(
    Guid Id,
    string Code,
    string FullName,
    Guid DepartmentId,
    string DepartmentName,
    Guid PositionId,
    string PositionName,
    string? Email,
    string? Phone,
    string? PhotoUrl,
    EmployeeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Query string of GET /api/employees.</summary>
public sealed class EmployeeListQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Matches code, full name (accent-insensitive) or email.</summary>
    public string? Search { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public EmployeeStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    /// <summary>code | fullName | department | position | status | createdAt; prefix "-" for descending.</summary>
    public string? Sort { get; init; }
}

/// <summary>Body of create and update.</summary>
public sealed record SaveEmployeeRequest(
    string Code,
    string FullName,
    Guid DepartmentId,
    Guid PositionId,
    string? Email,
    string? Phone,
    EmployeeStatus Status);
