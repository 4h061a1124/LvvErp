using LvvErp.Application.Common.Exceptions;
using LvvErp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LvvErp.Application.Common.Security;

/// <summary>
/// Who is acting, with the department of the employee linked to their account. Department-scoped
/// rules (raising and approving purchase requests) use <see cref="DepartmentId"/>; it is null when
/// the account has no linked (non-deleted) employee.
/// </summary>
public sealed record UserScope(Guid UserId, string UserName, IReadOnlyList<string> Roles, Guid? EmployeeId, Guid? DepartmentId)
{
    public bool HasRole(string role) => Roles.Contains(role);

    public bool Can(string permission) => RolePermissions.ForRoles(Roles).Contains(permission);
}

public interface IUserScopeProvider
{
    /// <summary>Loaded once per request from the database (roles may have changed since the token was issued).</summary>
    Task<UserScope> GetAsync(CancellationToken ct = default);
}

public sealed class UserScopeProvider(IAppDbContext db, ICurrentUser currentUser) : IUserScopeProvider
{
    private UserScope? _cached;

    public async Task<UserScope> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;

        var userId = currentUser.UserId ?? throw new UnauthorizedException("Not authenticated.");
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.UserName,
                u.Roles,
                EmployeeId = u.Employee != null ? u.EmployeeId : null,
                DepartmentId = u.Employee != null ? (Guid?)u.Employee.DepartmentId : null,
            })
            .SingleOrDefaultAsync(ct) ?? throw new UnauthorizedException("Not authenticated.");

        return _cached = new UserScope(userId, user.UserName, user.Roles, user.EmployeeId, user.DepartmentId);
    }
}
