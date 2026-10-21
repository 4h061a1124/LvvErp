// Mirrors backend LvvErp.Application.Common.Security.Permissions.
export const Permissions = {
  EmployeesView: 'Employees.View',
  EmployeesManage: 'Employees.Manage',
  UsersManage: 'Users.Manage',
} as const

export type Permission = (typeof Permissions)[keyof typeof Permissions]
