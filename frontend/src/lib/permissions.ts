// Mirrors backend LvvErp.Application.Common.Security.Permissions.
export const Permissions = {
  EmployeesView: 'Employees.View',
  EmployeesManage: 'Employees.Manage',
  UsersManage: 'Users.Manage',
  InventoryView: 'Inventory.View',
  InventoryManageCatalog: 'Inventory.ManageCatalog',
  InventoryEditDocuments: 'Inventory.EditDocuments',
  InventoryApproveDocuments: 'Inventory.ApproveDocuments',
  InventoryCancelDocuments: 'Inventory.CancelDocuments',
} as const

export type Permission = (typeof Permissions)[keyof typeof Permissions]
