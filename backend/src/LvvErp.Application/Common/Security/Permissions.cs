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

    /// <summary>Suppliers, every purchase request and order, purchasing reports.</summary>
    public const string PurchasingView = "Purchasing.View";
    /// <summary>Raise purchase requests for one's own department (and see that department's requests).</summary>
    public const string PurchasingCreateRequests = "Purchasing.CreateRequests";
    /// <summary>Approve requests of one's own department; never one's own request.</summary>
    public const string PurchasingApproveRequests = "Purchasing.ApproveRequests";
    public const string PurchasingManageSuppliers = "Purchasing.ManageSuppliers";
    public const string PurchasingManageOrders = "Purchasing.ManageOrders";
    /// <summary>Approve orders within one's approval limit (see PurchasingSettings); never one's own order.</summary>
    public const string PurchasingApproveOrders = "Purchasing.ApproveOrders";
    public const string PurchasingManageSettings = "Purchasing.ManageSettings";

    public static readonly IReadOnlyList<string> All =
    [
        EmployeesView, EmployeesManage, UsersManage,
        InventoryView, InventoryManageCatalog, InventoryEditDocuments, InventoryApproveDocuments, InventoryCancelDocuments,
        PurchasingView, PurchasingCreateRequests, PurchasingApproveRequests, PurchasingManageSuppliers,
        PurchasingManageOrders, PurchasingApproveOrders, PurchasingManageSettings,
    ];

    /// <summary>Approvals that commit money. Admin configures the system but never approves these.</summary>
    public static readonly IReadOnlyList<string> MoneyApprovals = [PurchasingApproveRequests, PurchasingApproveOrders];
}

/// <summary>The single place that decides which role grants which permission.</summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        [Roles.Admin] = [.. Permissions.All.Except(Permissions.MoneyApprovals)],
        [Roles.HR] = [Permissions.EmployeesView, Permissions.EmployeesManage, Permissions.InventoryView, Permissions.PurchasingCreateRequests],
        [Roles.Employee] = [Permissions.EmployeesView, Permissions.InventoryView, Permissions.PurchasingCreateRequests],
        [Roles.Storekeeper] =
        [
            Permissions.EmployeesView,
            Permissions.InventoryView,
            Permissions.InventoryManageCatalog,
            Permissions.InventoryEditDocuments,
            Permissions.InventoryApproveDocuments,
            Permissions.PurchasingView,
            Permissions.PurchasingCreateRequests,
        ],
        [Roles.Purchaser] =
        [
            Permissions.EmployeesView,
            Permissions.InventoryView,
            Permissions.PurchasingView,
            Permissions.PurchasingCreateRequests,
            Permissions.PurchasingManageSuppliers,
            Permissions.PurchasingManageOrders,
        ],
        [Roles.DepartmentHead] =
        [
            Permissions.EmployeesView,
            Permissions.InventoryView,
            Permissions.PurchasingView,
            Permissions.PurchasingCreateRequests,
            Permissions.PurchasingApproveRequests,
            Permissions.PurchasingApproveOrders,
        ],
        [Roles.Director] =
        [
            Permissions.EmployeesView,
            Permissions.InventoryView,
            Permissions.PurchasingView,
            Permissions.PurchasingCreateRequests,
            Permissions.PurchasingApproveRequests,
            Permissions.PurchasingApproveOrders,
        ],
    };

    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        roles.SelectMany(r => Map.GetValueOrDefault(r, [])).Distinct().Order().ToList();

    public static IReadOnlyList<string> RolesWith(string permission) =>
        Map.Where(kv => kv.Value.Contains(permission)).Select(kv => kv.Key).ToList();
}
