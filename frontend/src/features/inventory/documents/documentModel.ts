import { z } from 'zod'
import type { PurchaseOrder } from '@/features/purchasing/types'
import { formatQuantityInput, hasAtMostThreeDecimals, parseQuantity, todayIso } from '@/lib/format'
import type { DocumentInput, DocumentReason, DocumentType, StockDocument } from './types'

export interface MaterialRef {
  id: string
  code: string
  name: string
  unitName: string
  trackLots: boolean
}

/** "locationId|lotId" of an available-stock row ("" for a missing part). */
export function sourceKey(locationId: string | null, lotId: string | null): string {
  return `${locationId ?? ''}|${lotId ?? ''}`
}

export function parseSourceKey(key: string): { locationId: string | null; lotId: string | null } {
  const [locationId, lotId] = key.split('|')
  return { locationId: locationId || null, lotId: lotId || null }
}

const lineSchema = z.object({
  key: z.string(),
  material: z.custom<MaterialRef | null>(),
  locationId: z.string(),
  toLocationId: z.string(),
  /** Issue/transfer: the available-stock row taken from. */
  source: z.string(),
  lotNumber: z.string().trim().max(50, 'validation.tooLong'),
  manufactureDate: z.string(),
  expiryDate: z.string(),
  quantity: z.string(),
  /** Receipts from a purchase order: the order line received. */
  purchaseOrderLineId: z.string(),
})
export type LineValues = z.infer<typeof lineSchema>

const formSchema = z.object({
  documentDate: z.string().min(1, 'validation.required'),
  warehouseId: z.string().min(1, 'validation.required'),
  toWarehouseId: z.string(),
  reason: z.string(),
  partner: z.string().trim().max(200, 'validation.tooLong'),
  departmentId: z.string(),
  employee: z.custom<{ id: string; code: string; fullName: string; departmentId: string } | null>(),
  note: z.string().trim().max(500, 'validation.tooLong'),
  purchaseOrder: z.custom<{ id: string; number: string } | null>(),
  lines: z.array(lineSchema),
})
export type DocumentFormValues = z.infer<typeof formSchema>

/** Same rules as the backend draft validator; messages are i18n keys, paths match the form fields. */
export function documentSchema(type: DocumentType) {
  return formSchema.superRefine((v, ctx) => {
    const issue = (path: (string | number)[], message: string) => ctx.addIssue({ code: 'custom', path, message })

    if (v.documentDate > todayIso()) issue(['documentDate'], 'validation.futureDate')
    if (type === 'Transfer' && !v.toWarehouseId) issue(['toWarehouseId'], 'validation.required')
    if (type !== 'Transfer' && !v.reason) issue(['reason'], 'validation.required')

    v.lines.forEach((line, i) => {
      if (!line.material) {
        issue(['lines', i, 'material'], 'validation.required')
        return
      }
      const qty = parseQuantity(line.quantity)
      if (qty === null || qty <= 0 || !hasAtMostThreeDecimals(qty)) issue(['lines', i, 'quantity'], 'validation.positiveQuantity')

      if (type === 'Receipt') {
        if (line.material.trackLots && !line.lotNumber.trim()) issue(['lines', i, 'lotNumber'], 'inventory.documents.lotRequired')
        if (line.manufactureDate && line.expiryDate && line.expiryDate < line.manufactureDate)
          issue(['lines', i, 'expiryDate'], 'validation.expiryBeforeManufacture')
      } else {
        if (!line.source) issue(['lines', i, 'source'], 'inventory.documents.sourceRequired')
        if (type === 'Transfer' && v.warehouseId === v.toWarehouseId && parseSourceKey(line.source).locationId === (line.toLocationId || null))
          issue(['lines', i, 'toLocationId'], 'inventory.documents.sameAsSource')
      }
    })
  })
}

let nextKey = 1
export function emptyLine(material: MaterialRef | null = null): LineValues {
  return {
    key: `new-${nextKey++}`,
    material,
    locationId: '',
    toLocationId: '',
    source: '',
    lotNumber: '',
    manufactureDate: '',
    expiryDate: '',
    quantity: '',
    purchaseOrderLineId: '',
  }
}

export function newDocumentValues(type: DocumentType): DocumentFormValues {
  return {
    documentDate: todayIso(),
    warehouseId: '',
    toWarehouseId: '',
    reason: type === 'Receipt' ? 'Purchase' : type === 'Issue' ? 'Production' : '',
    partner: '',
    departmentId: '',
    employee: null,
    note: '',
    purchaseOrder: null,
    lines: [],
  }
}

export function documentToValues(d: StockDocument): DocumentFormValues {
  return {
    documentDate: d.documentDate,
    warehouseId: d.warehouseId,
    toWarehouseId: d.toWarehouseId ?? '',
    reason: d.reason ?? '',
    partner: d.partner ?? '',
    departmentId: d.departmentId ?? '',
    employee: d.employeeId ? { id: d.employeeId, code: '', fullName: d.employeeName ?? '', departmentId: d.departmentId ?? '' } : null,
    note: d.note ?? '',
    purchaseOrder: d.purchaseOrderId ? { id: d.purchaseOrderId, number: d.purchaseOrderNumber ?? '' } : null,
    lines: d.lines.map((l) => ({
      key: l.id,
      material: { id: l.materialId, code: l.materialCode, name: l.materialName, unitName: l.unitName, trackLots: l.trackLots },
      locationId: l.locationId ?? '',
      toLocationId: l.toLocationId ?? '',
      source: d.type === 'Receipt' ? '' : sourceKey(l.locationId, l.lotId),
      lotNumber: d.type === 'Receipt' ? (l.lotNumber ?? '') : '',
      manufactureDate: d.type === 'Receipt' ? (l.manufactureDate ?? '') : '',
      expiryDate: d.type === 'Receipt' ? (l.expiryDate ?? '') : '',
      quantity: String(l.quantity),
      purchaseOrderLineId: l.purchaseOrderLineId ?? '',
    })),
  }
}

export function valuesToInput(type: DocumentType, v: DocumentFormValues): DocumentInput {
  return {
    documentDate: v.documentDate,
    warehouseId: v.warehouseId,
    ...(type === 'Transfer' ? { toWarehouseId: v.toWarehouseId } : { reason: v.reason as DocumentReason }),
    ...(type === 'Receipt' ? { partner: v.partner || null } : {}),
    ...(type === 'Issue' ? { departmentId: v.departmentId || null, employeeId: v.employee?.id ?? null } : {}),
    note: v.note || null,
    ...(type === 'Receipt' && v.purchaseOrder ? { purchaseOrderId: v.purchaseOrder.id } : {}),
    lines: v.lines.map((l) => {
      const quantity = parseQuantity(l.quantity) ?? 0
      if (type === 'Receipt') {
        const lot = l.material?.trackLots
        return {
          materialId: l.material!.id,
          locationId: l.locationId || null,
          lotNumber: lot ? l.lotNumber.trim() : null,
          manufactureDate: lot ? l.manufactureDate || null : null,
          expiryDate: lot ? l.expiryDate || null : null,
          quantity,
          ...(v.purchaseOrder ? { purchaseOrderLineId: l.purchaseOrderLineId } : {}),
        }
      }
      const { locationId, lotId } = parseSourceKey(l.source)
      return {
        materialId: l.material!.id,
        locationId,
        lotId,
        ...(type === 'Transfer' ? { toLocationId: l.toLocationId || null } : {}),
        quantity,
      }
    }),
  }
}

/**
 * A new purchase receipt for what is still due on an approved order: one line per order line,
 * quantity = ordered − received (lines already received in full are left out).
 */
export function receiptFromOrder(order: PurchaseOrder, language: string): DocumentFormValues {
  return {
    ...newDocumentValues('Receipt'),
    warehouseId: order.warehouseId,
    reason: 'Purchase',
    partner: order.supplier.name,
    purchaseOrder: { id: order.id, number: order.number },
    lines: order.lines
      .filter((l) => l.receivedQuantity < l.quantity)
      .map((l) => ({
        ...emptyLine({ id: l.materialId, code: l.materialCode, name: l.materialName, unitName: l.unitName, trackLots: l.trackLots }),
        quantity: formatQuantityInput(Number((l.quantity - l.receivedQuantity).toFixed(3)), language),
        purchaseOrderLineId: l.id,
      })),
  }
}

/** Another line for the same order line, e.g. when the goods arrive in two lots. */
export function splitLine(line: LineValues): LineValues {
  return { ...emptyLine(line.material), locationId: line.locationId, purchaseOrderLineId: line.purchaseOrderLineId }
}
