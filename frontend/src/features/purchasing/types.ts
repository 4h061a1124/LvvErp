// Mirrors the purchasing DTOs of the backend (LvvErp.Application.Features.Purchasing).

export interface Supplier {
  id: string
  code: string
  name: string
  taxCode: string | null
  address: string | null
  contactName: string | null
  phone: string | null
  email: string | null
  paymentTermDays: number
  paymentTerms: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}

export interface SupplierInput {
  code: string
  name: string
  taxCode: string | null
  address: string | null
  contactName: string | null
  phone: string | null
  email: string | null
  paymentTermDays: number
  paymentTerms: string | null
  isActive: boolean
}

export interface SupplierListParams {
  search?: string
  isActive?: boolean
  page: number
  pageSize: number
  sort?: string
}

export interface PurchasingSettings {
  directorApprovalThreshold: number
  purchasingDepartmentId: string | null
  purchasingDepartmentName: string | null
  companyName: string | null
  companyAddress: string | null
  companyTaxCode: string | null
  companyPhone: string | null
  updatedAt: string | null
  updatedBy: string | null
}

export const REQUEST_STATUSES = [
  'Draft',
  'Submitted',
  'Approved',
  'Rejected',
  'PartiallyOrdered',
  'Ordered',
  'Closed',
  'Cancelled',
] as const
export type RequestStatus = (typeof REQUEST_STATUSES)[number]

export interface PurchaseRequestSummary {
  id: string
  number: string
  status: RequestStatus
  requestDate: string
  neededDate: string | null
  departmentId: string
  departmentName: string
  requesterName: string
  note: string | null
  lineCount: number
  createdAt: string
  submittedAt: string | null
}

export interface RequestActions {
  canEdit: boolean
  canDelete: boolean
  canSubmit: boolean
  canApprove: boolean
  canCancel: boolean
  canClose: boolean
}

export interface PurchaseRequestLine {
  id: string
  lineNo: number
  materialId: string
  materialCode: string
  materialName: string
  unitName: string
  quantity: number
  orderedQuantity: number
  note: string | null
}

export interface RelatedOrder {
  id: string
  number: string
  status: OrderStatus
}

export interface PurchaseRequest {
  id: string
  number: string
  status: RequestStatus
  requestDate: string
  neededDate: string | null
  departmentId: string
  departmentName: string
  requesterId: string
  requesterName: string
  note: string | null
  createdAt: string
  submittedAt: string | null
  approvedAt: string | null
  approvedBy: string | null
  rejectedAt: string | null
  rejectedBy: string | null
  rejectReason: string | null
  closedAt: string | null
  closedBy: string | null
  closeReason: string | null
  cancelledAt: string | null
  cancelledBy: string | null
  cancelReason: string | null
  lines: PurchaseRequestLine[]
  orders: RelatedOrder[]
  actions: RequestActions
}

export interface PurchaseRequestInput {
  requestDate: string
  neededDate: string | null
  note: string | null
  lines: { materialId: string; quantity: number; note: string | null }[]
}

export interface RequestListParams {
  search?: string
  status?: RequestStatus
  departmentId?: string
  from?: string
  to?: string
  mine?: boolean
  page: number
  pageSize: number
  sort?: string
}

export const ORDER_STATUSES = [
  'Draft',
  'Submitted',
  'Approved',
  'Rejected',
  'PartiallyReceived',
  'Received',
  'Closed',
  'Cancelled',
] as const
export type OrderStatus = (typeof ORDER_STATUSES)[number]

export type ApprovalLevel = 'PurchasingHead' | 'Director'

export const VAT_RATES = ['Exempt', 'Zero', 'Five', 'Eight', 'Ten'] as const
export type VatRate = (typeof VAT_RATES)[number]

export interface PurchaseOrderSummary {
  id: string
  number: string
  status: OrderStatus
  orderDate: string
  expectedDate: string | null
  supplierId: string
  supplierName: string
  warehouseName: string
  vatRate: VatRate
  total: number
  lineCount: number
  approvalLevel: ApprovalLevel | null
  isOverdue: boolean
  createdBy: string | null
  createdAt: string
  submittedAt: string | null
}

export interface ApprovalInbox {
  requests: PurchaseRequestSummary[]
  orders: PurchaseOrderSummary[]
}

export interface OrderActions {
  canEdit: boolean
  canDelete: boolean
  canSubmit: boolean
  canApprove: boolean
  canCancel: boolean
  canClose: boolean
}

export interface OrderLineSource {
  requestLineId: string
  requestId: string
  requestNumber: string
  requestLineNo: number
  quantity: number
}

export interface PurchaseOrderLine {
  id: string
  lineNo: number
  materialId: string
  materialCode: string
  materialName: string
  specification: string | null
  unitName: string
  quantity: number
  unitPrice: number
  amount: number
  note: string | null
  overOrderReason: string | null
  receivedQuantity: number
  receiptTolerancePercent: number
  trackLots: boolean
  sources: OrderLineSource[]
}

export interface OrderSupplier {
  id: string
  code: string
  name: string
  taxCode: string | null
  address: string | null
  contactName: string | null
  phone: string | null
  email: string | null
}

export interface OrderReceipt {
  id: string
  number: string
  status: 'Draft' | 'Approved' | 'Cancelled'
  documentDate: string
}

export interface PurchaseOrder {
  id: string
  number: string
  status: OrderStatus
  orderDate: string
  expectedDate: string | null
  supplier: OrderSupplier
  warehouseId: string
  warehouseName: string
  paymentTermDays: number
  vatRate: VatRate
  note: string | null
  subtotal: number
  vatAmount: number
  total: number
  approvalLevel: ApprovalLevel | null
  isOverdue: boolean
  createdAt: string
  createdBy: string | null
  submittedAt: string | null
  submittedBy: string | null
  approvedAt: string | null
  approvedBy: string | null
  rejectedAt: string | null
  rejectedBy: string | null
  rejectReason: string | null
  closedAt: string | null
  closedBy: string | null
  closeReason: string | null
  cancelledAt: string | null
  cancelledBy: string | null
  cancelReason: string | null
  lines: PurchaseOrderLine[]
  receipts: OrderReceipt[]
  actions: OrderActions
}

export interface PurchaseOrderInput {
  supplierId: string
  orderDate: string
  expectedDate: string | null
  warehouseId: string
  paymentTermDays: number
  vatRate: VatRate
  note: string | null
  lines: {
    materialId: string
    quantity: number
    unitPrice: number
    sourceLineIds: string[]
    note: string | null
    overOrderReason: string | null
  }[]
}

export interface OrderListParams {
  search?: string
  status?: OrderStatus
  supplierId?: string
  overdueOnly?: boolean
  from?: string
  to?: string
  page: number
  pageSize: number
  sort?: string
}

/** An approved request line still to be ordered. */
export interface OrderableLine {
  requestLineId: string
  requestId: string
  requestNumber: string
  departmentName: string
  requesterName: string
  neededDate: string | null
  lineNo: number
  materialId: string
  materialCode: string
  materialName: string
  unitName: string
  quantity: number
  orderedQuantity: number
  remainingQuantity: number
  note: string | null
}

export interface LastPrice {
  materialId: string
  unitPrice: number
  supplierId: string
  supplierName: string
  sameSupplier: boolean
  orderNumber: string
  orderDate: string
}

export interface PriceHistoryRow {
  orderId: string
  orderNumber: string
  orderDate: string
  status: OrderStatus
  supplierId: string
  supplierName: string
  quantity: number
  unitPrice: number
  vatRate: VatRate
}

export interface StatusCount<TStatus> {
  status: TStatus
  count: number
  total: number
}

export interface PurchasingSummary {
  requests: StatusCount<RequestStatus>[]
  orders: StatusCount<OrderStatus>[]
  overdueOrders: number
  openOrderValue: number
}

export interface OverdueLine {
  orderId: string
  orderNumber: string
  supplierName: string
  expectedDate: string
  daysLate: number
  lineNo: number
  materialCode: string
  materialName: string
  unitName: string
  ordered: number
  received: number
  remaining: number
}
