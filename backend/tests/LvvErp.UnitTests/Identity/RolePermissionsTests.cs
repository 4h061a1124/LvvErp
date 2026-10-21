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
    public void Employee_can_only_view_employees() =>
        Assert.Equal([Permissions.EmployeesView], RolePermissions.ForRoles([Roles.Employee]));

    [Fact]
    public void Every_signed_in_role_can_view_employees() =>
        Assert.Equal(Roles.All.Order(), RolePermissions.RolesWith(Permissions.EmployeesView).Order());

    [Fact]
    public void Multiple_roles_merge_without_duplicates() =>
        Assert.Equal(
            RolePermissions.ForRoles([Roles.HR]),
            RolePermissions.ForRoles([Roles.HR, Roles.Employee]));

    [Fact]
    public void Every_permission_is_granted_to_some_role() =>
        Assert.All(Permissions.All, p => Assert.NotEmpty(RolePermissions.RolesWith(p)));
}
