using LvvErp.Application.Common.Security;
using LvvErp.Domain.Features.Identity;

namespace LvvErp.UnitTests.Identity;

public sealed class RolePermissionsTests
{
    [Fact]
    public void Admin_has_every_permission_except_money_approvals() =>
        Assert.Equal(Permissions.All.Except(Permissions.MoneyApprovals).Order(), RolePermissions.ForRoles([Roles.Admin]));

    [Fact]
    public void HR_can_view_and_manage_employees_but_not_users()
    {
        var permissions = RolePermissions.ForRoles([Roles.HR]);

        Assert.Contains(Permissions.EmployeesView, permissions);
        Assert.Contains(Permissions.EmployeesManage, permissions);
        Assert.DoesNotContain(Permissions.UsersManage, permissions);
    }

    [Fact]
    public void Employee_can_view_and_raise_purchase_requests() =>
        Assert.Equal(
            [Permissions.EmployeesView, Permissions.InventoryView, Permissions.PurchasingCreateRequests],
            RolePermissions.ForRoles([Roles.Employee]));

    [Fact]
    public void Every_signed_in_role_can_view_employees_and_inventory()
    {
        Assert.Equal(Roles.All.Order(), RolePermissions.RolesWith(Permissions.EmployeesView).Order());
        Assert.Equal(Roles.All.Order(), RolePermissions.RolesWith(Permissions.InventoryView).Order());
    }

    [Fact]
    public void Storekeeper_manages_catalog_and_documents_but_cannot_cancel()
    {
        var permissions = RolePermissions.ForRoles([Roles.Storekeeper]);

        Assert.Contains(Permissions.InventoryManageCatalog, permissions);
        Assert.Contains(Permissions.InventoryEditDocuments, permissions);
        Assert.Contains(Permissions.InventoryApproveDocuments, permissions);
        Assert.DoesNotContain(Permissions.InventoryCancelDocuments, permissions);
        Assert.DoesNotContain(Permissions.EmployeesManage, permissions);
        Assert.DoesNotContain(Permissions.UsersManage, permissions);
    }

    [Fact]
    public void Only_admin_can_cancel_documents() =>
        Assert.Equal([Roles.Admin], RolePermissions.RolesWith(Permissions.InventoryCancelDocuments));

    [Fact]
    public void Only_admin_and_storekeeper_change_inventory()
    {
        foreach (var permission in new[]
                 {
                     Permissions.InventoryManageCatalog, Permissions.InventoryEditDocuments, Permissions.InventoryApproveDocuments,
                 })
        {
            Assert.Equal([Roles.Admin, Roles.Storekeeper], RolePermissions.RolesWith(permission).Order());
        }
    }

    [Fact]
    public void Money_approvals_go_only_to_department_heads_and_directors()
    {
        foreach (var permission in Permissions.MoneyApprovals)
            Assert.Equal([Roles.DepartmentHead, Roles.Director], RolePermissions.RolesWith(permission).Order());
    }

    [Fact]
    public void Every_signed_in_role_can_raise_purchase_requests() =>
        Assert.Equal(Roles.All.Order(), RolePermissions.RolesWith(Permissions.PurchasingCreateRequests).Order());

    [Fact]
    public void Purchaser_manages_suppliers_and_orders_but_approves_nothing()
    {
        var permissions = RolePermissions.ForRoles([Roles.Purchaser]);

        Assert.Contains(Permissions.PurchasingManageSuppliers, permissions);
        Assert.Contains(Permissions.PurchasingManageOrders, permissions);
        Assert.DoesNotContain(Permissions.PurchasingApproveRequests, permissions);
        Assert.DoesNotContain(Permissions.PurchasingApproveOrders, permissions);
        Assert.DoesNotContain(Permissions.PurchasingManageSettings, permissions);
    }

    [Fact]
    public void Storekeeper_sees_purchasing_but_cannot_manage_suppliers()
    {
        var permissions = RolePermissions.ForRoles([Roles.Storekeeper]);

        Assert.Contains(Permissions.PurchasingView, permissions);
        Assert.DoesNotContain(Permissions.PurchasingManageSuppliers, permissions);
        Assert.DoesNotContain(Permissions.PurchasingManageOrders, permissions);
    }

    [Fact]
    public void Only_admin_changes_purchasing_settings() =>
        Assert.Equal([Roles.Admin], RolePermissions.RolesWith(Permissions.PurchasingManageSettings));

    [Fact]
    public void Multiple_roles_merge_without_duplicates() =>
        Assert.Equal(
            RolePermissions.ForRoles([Roles.HR]),
            RolePermissions.ForRoles([Roles.HR, Roles.Employee]));

    [Fact]
    public void Every_permission_is_granted_to_some_role() =>
        Assert.All(Permissions.All, p => Assert.NotEmpty(RolePermissions.RolesWith(p)));
}
