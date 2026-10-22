export type DocumentType = 'Receipt' | 'Issue' | 'Transfer'
export type DocumentStatus = 'Draft' | 'Approved' | 'Cancelled'
export type DocumentReason = 'Purchase' | 'ProductionReturn' | 'OpeningBalance' | 'Production' | 'Consumption' | 'Other'

export const DOCUMENT_STATUSES: DocumentStatus[] = ['Draft', 'Approved', 'Cancelled']

// Mirrors StockDocument.IsReasonAllowed on the backend.
export const REASONS: Record<DocumentType, DocumentReason[]> = {
  Receipt: ['Purchase', 'ProductionReturn', 'OpeningBalance', 'Other'],
  Issue: ['Production', 'Consumption', 'Other'],
  Transfer: [],
}

/** API resource and app route per document type. */
export const DOCUMENT_PATHS: Record<DocumentType, { api: string; route: string }> = {
  Receipt: { api: '/stock-receipts', route: '/inventory/receipts' },
  Issue: { api: '/stock-issues', route: '/inventory/issues' },
  Transfer: { api: '/stock-transfers', route: '/inventory/transfers' },
}

export interface StockDocumentLine {
  id: string
  lineNo: number
  materialId: string
  materialCode: string
  materialName: string
  unitName: string
  trackLots: boolean
  locationId: string | null
  locationCode: string | null
  toLocationId: string | null
  toLocationCode: string | null
  lotId: string | null
  lotNumber: string | null
  manufactureDate: string | null
  expiryDate: string | null
  quantity: number
  note: string | null
  purchaseOrderLineId: string | null
}

export interface StockDocument {
  id: string
  number: string
  type: DocumentType
  status: DocumentStatus
  documentDate: string
  warehouseId: string
  warehouseName: string
  toWarehouseId: string | null
  toWarehouseName: string | null
  reason: DocumentReason | null
  partner: string | null
  departmentId: string | null
  departmentName: string | null
  employeeId: string | null
  employeeName: string | null
  note: string | null
  createdAt: string
  createdBy: string | null
  approvedAt: string | null
  approvedBy: string | null
  cancelledAt: string | null
  cancelledBy: string | null
  cancelReason: string | null
  /** Receipt of goods on a purchase order. */
  purchaseOrderId: string | null
  purchaseOrderNumber: string | null
  lines: StockDocumentLine[]
}

export interface StockDocumentSummary {
  id: string
  number: string
  type: DocumentType
  status: DocumentStatus
  documentDate: string
  warehouseName: string
  toWarehouseName: string | null
  reason: DocumentReason | null
  partner: string | null
  departmentName: string | null
  employeeName: string | null
  lineCount: number
  createdBy: string | null
  createdAt: string
  approvedAt: string | null
  purchaseOrderId: string | null
  purchaseOrderNumber: string | null
}

export interface DocumentListParams {
  status?: DocumentStatus
  warehouseId?: string
  from?: string
  to?: string
  search?: string
  page: number
  pageSize: number
}

/** Body of create/update; field names match the backend requests of every type. */
export interface DocumentInput {
  documentDate: string
  warehouseId: string
  toWarehouseId?: string
  reason?: DocumentReason
  partner?: string | null
  departmentId?: string | null
  employeeId?: string | null
  note: string | null
  purchaseOrderId?: string | null
  lines: {
    materialId: string
    locationId: string | null
    toLocationId?: string | null
    lotId?: string | null
    lotNumber?: string | null
    manufactureDate?: string | null
    expiryDate?: string | null
    quantity: number
    note?: string | null
    purchaseOrderLineId?: string | null
  }[]
}

/** 409 OverReceipt details, per purchase order line. */
export interface OverReceiptLine {
  lineNo: number
  materialCode: string
  ordered: number
  received: number
  receiving: number
  allowed: number
}

export interface StockShortage {
  lineNo: number
  materialCode: string
  lotNumber: string | null
  requested: number
  available: number
}

export interface AvailableStock {
  warehouseId: string
  locationId: string | null
  locationCode: string | null
  lotId: string | null
  lotNumber: string | null
  expiryDate: string | null
  quantity: number
}

export interface Lot {
  id: string
  lotNumber: string
  manufactureDate: string | null
  expiryDate: string | null
  quantity: number
}
