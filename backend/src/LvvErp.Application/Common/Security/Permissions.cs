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

    /// <summary>Catalog, stock, stock card and documents, read-only.</summary>
    public const string InventoryView = "Inventory.View";
    public const string InventoryManageCatalog = "Inventory.ManageCatalog";
    /// <summary>Create, edit and delete draft documents; import opening balances.</summary>
    public const string InventoryEditDocuments = "Inventory.EditDocuments";
    public const string InventoryApproveDocuments = "Inventory.ApproveDocuments";
    /// <summary>Cancel an approved document (writes reversing entries).</summary>
    public const string InventoryCancelDocuments = "Inventory.CancelDocuments";

    public static readonly IReadOnlyList<string> All =
    [
        EmployeesView, EmployeesManage, UsersManage,
        InventoryView, InventoryManageCatalog, InventoryEditDocuments, InventoryApproveDocuments, InventoryCancelDocuments,
    ];
}

/// <summary>The single place that decides which role grants which permission.</summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        [Roles.Admin] = [.. Permissions.All],
        [Roles.HR] = [Permissions.EmployeesView, Permissions.EmployeesManage, Permissions.InventoryView],
        [Roles.Employee] = [Permissions.EmployeesView, Permissions.InventoryView],
        [Roles.Storekeeper] =
        [
            Permissions.EmployeesView,
            Permissions.InventoryView,
            Permissions.InventoryManageCatalog,
            Permissions.InventoryEditDocuments,
            Permissions.InventoryApproveDocuments,
        ],
    };

    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        roles.SelectMany(r => Map.GetValueOrDefault(r, [])).Distinct().Order().ToList();

    public static IReadOnlyList<string> RolesWith(string permission) =>
        Map.Where(kv => kv.Value.Contains(permission)).Select(kv => kv.Key).ToList();
}
