using LvvErp.Domain.Features.Identity;

namespace LvvErp.Application.Common.Security;

/// <summary>
/// Permission names double as ASP.NET authorization policy names.
/// Controllers use <c>[Authorize(Policy = Permissions.X)]</c>; the frontend receives the
/// user's permission list from /api/auth/me to show or hide actions.
/// </summary>
public static class Permissions
{
    public const string EmployeesView = "Employees.View";
    public const string EmployeesManage = "Employees.Manage";
    public const string UsersManage = "Users.Manage";

    public static readonly IReadOnlyList<string> All = [EmployeesView, EmployeesManage, UsersManage];
}

/// <summary>The single place that decides which role grants which permission.</summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        [Roles.Admin] = [.. Permissions.All],
        [Roles.HR] = [Permissions.EmployeesView, Permissions.EmployeesManage],
        [Roles.Employee] = [Permissions.EmployeesView],
    };

    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        roles.SelectMany(r => Map.GetValueOrDefault(r, [])).Distinct().Order().ToList();

    public static IReadOnlyList<string> RolesWith(string permission) =>
        Map.Where(kv => kv.Value.Contains(permission)).Select(kv => kv.Key).ToList();
}
