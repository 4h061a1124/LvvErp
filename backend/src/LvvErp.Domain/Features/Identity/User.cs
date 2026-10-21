using LvvErp.Domain.Common;
using LvvErp.Domain.Features.Employees;

namespace LvvErp.Domain.Features.Identity;

/// <summary>An account that can sign in. Users are deactivated, never deleted.</summary>
public class User : AuditableEntity
{
    /// <summary>Always stored normalized (see <see cref="NormalizeUserName"/>).</summary>
    public required string UserName { get; set; }
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Embedded in issued tokens. Rotating it (password change, role change, deactivation)
    /// invalidates every token issued before.
    /// </summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>Optional link to the employee record of this account (one account per employee).</summary>
    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public static string NormalizeUserName(string userName) => userName.Trim().ToLowerInvariant();

    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
}
