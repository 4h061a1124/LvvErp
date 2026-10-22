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
  PurchasingView: 'Purchasing.View',
  PurchasingCreateRequests: 'Purchasing.CreateRequests',
  PurchasingApproveRequests: 'Purchasing.ApproveRequests',
  PurchasingManageSuppliers: 'Purchasing.ManageSuppliers',
  PurchasingManageOrders: 'Purchasing.ManageOrders',
  PurchasingApproveOrders: 'Purchasing.ApproveOrders',
  PurchasingManageSettings: 'Purchasing.ManageSettings',
} as const

export type Permission = (typeof Permissions)[keyof typeof Permissions]

/** Mirrors backend Roles.All, in the order shown when assigning roles. */
export const ROLES = ['Admin', 'HR', 'Employee', 'Storekeeper', 'Purchaser', 'DepartmentHead', 'Director'] as const
export type Role = (typeof ROLES)[number]
