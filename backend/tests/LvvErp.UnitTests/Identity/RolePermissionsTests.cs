using LvvErp.Application.Common.Security;
using LvvErp.Domain.Features.Identity;

namespace LvvErp.UnitTests.Identity;

public sealed class RolePermissionsTests
{
    [Fact]
    public void Admin_has_every_permission() =>
        Assert.Equal(Permissions.All.Order(), RolePermissions.ForRoles([Roles.Admin]));

    [Fact]
    public void HR_can_view_and_manage_employees_but_not_users()
    {
        var permissions = RolePermissions.ForRoles([Roles.HR]);

        Assert.Contains(Permissions.EmployeesView, permissions);
        Assert.Contains(Permissions.EmployeesManage, permissions);
        Assert.DoesNotContain(Permissions.UsersManage, permissions);
    }

    [Fact]
    public void Employee_can_only_view() =>
        Assert.Equal([Permissions.EmployeesView, Permissions.InventoryView], RolePermissions.ForRoles([Roles.Employee]));

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
    public void Multiple_roles_merge_without_duplicates() =>
        Assert.Equal(
            RolePermissions.ForRoles([Roles.HR]),
            RolePermissions.ForRoles([Roles.HR, Roles.Employee]));

    [Fact]
    public void Every_permission_is_granted_to_some_role() =>
        Assert.All(Permissions.All, p => Assert.NotEmpty(RolePermissions.RolesWith(p)));
}
