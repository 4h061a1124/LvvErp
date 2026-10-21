using LvvErp.Domain.Common;

namespace LvvErp.Domain.Features.Employees;

public class Employee : AuditableEntity, ISoftDeletable
{
    public const int CodeMaxLength = 20;
    public const int FullNameMaxLength = 200;
    public const int EmailMaxLength = 200;
    public const int PhoneMaxLength = 20;

    /// <summary>Employee code (mã NV), unique among non-deleted employees. Stored upper-case.</summary>
    public required string Code { get; set; }
    public required string FullName { get; set; }

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public Guid PositionId { get; set; }
    public Position Position { get; set; } = null!;

    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Storage-relative path of the photo, e.g. "employees/0192....webp".</summary>
    public string? PhotoPath { get; set; }

    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}

public enum EmployeeStatus
{
    Active = 1,
    OnLeave = 2,
    Resigned = 3,
}
