import { z } from 'zod'
import { formatQuantityInput, hasAtMostThreeDecimals, parseQuantity, todayIso } from '@/lib/format'
import type { OrderableLine, PurchaseOrder, PurchaseOrderInput, VatRate } from '../types'

export interface OrderMaterial {
  id: string
  code: string
  name: string
  unitName: string
}

/** A request line the order line draws from. */
export interface OrderSource {
  requestLineId: string
  requestNumber: string
  requestLineNo: number
  /** What the request line can still give to this order: its remaining plus what this order already holds. */
  available: number
}

export interface SupplierRef {
  id: string
  code: string
  name: string
  paymentTermDays: number
}

const lineSchema = z.object({
  key: z.string(),
  material: z.custom<OrderMaterial>(),
  sources: z.custom<OrderSource[]>(),
  quantity: z.string(),
  unitPrice: z.string(),
  note: z.string().trim().max(200, 'validation.tooLong'),
  overOrderReason: z.string().trim().max(500, 'validation.tooLong'),
})
export type OrderLineValues = z.infer<typeof lineSchema>

const formSchema = z.object({
  supplier: z.custom<SupplierRef | null>(),
  orderDate: z.string().min(1, 'validation.required'),
  expectedDate: z.string(),
  warehouseId: z.string().min(1, 'validation.required'),
  paymentTermDays: z.string(),
  vatRate: z.custom<VatRate>(),
  note: z.string().trim().max(500, 'validation.tooLong'),
  lines: z.array(lineSchema),
})
export type OrderFormValues = z.infer<typeof formSchema>

export const VAT_PERCENT: Record<VatRate, number> = { Exempt: 0, Zero: 0, Five: 5, Eight: 8, Ten: 10 }

/**
 * Unit price as typed: "12.500" or "12.500,5" in Vietnamese (dot groups thousands), "12,500.5" in English.
 * Up to 2 decimals. Null when invalid.
 */
export function parsePrice(text: string, language: string): number | null {
  let s = text.trim().replace(/\s/g, '')
  s = language === 'vi' ? s.replace(/\./g, '').replace(',', '.') : s.replace(/,/g, '')
  if (!/^\d{1,13}(\.\d{1,2})?$/.test(s)) return null
  return Number(s)
}

/** Same rounding as the backend: whole đồng, half away from zero (amounts are never negative). */
export function roundMoney(value: number): number {
  return Math.round(Number(value.toFixed(6)))
}

export function lineAmount(quantity: number | null, unitPrice: number | null): number {
  return quantity === null || unitPrice === null ? 0 : roundMoney(quantity * unitPrice)
}

/** VAT once on the subtotal, as on a VAT invoice. */
export function orderTotals(amounts: number[], vatRate: VatRate) {
  const subtotal = amounts.reduce((sum, a) => sum + a, 0)
  const vat = roundMoney((subtotal * VAT_PERCENT[vatRate]) / 100)
  return { subtotal, vat, total: subtotal + vat }
}

/** How much the request lines behind an order line still need. */
export function requested(line: Pick<OrderLineValues, 'sources'>): number {
  return Number(line.sources.reduce((sum, s) => sum + Math.max(0, s.available), 0).toFixed(3))
}

/** Ordering more than the requests still need requires a reason (the approver sees it). */
export function isOverOrdered(line: Pick<OrderLineValues, 'sources' | 'quantity'>): boolean {
  const qty = parseQuantity(line.quantity)
  return qty !== null && qty > requested(line)
}

/** Same rules as the backend SavePurchaseOrderRequestValidator; messages are i18n keys. */
export function orderSchema(language: string) {
  return formSchema.superRefine((v, ctx) => {
    const issue = (path: (string | number)[], message: string) => ctx.addIssue({ code: 'custom', path, message })
    if (!v.supplier) issue(['supplier'], 'validation.required')
    if (v.expectedDate && v.orderDate && v.expectedDate < v.orderDate) issue(['expectedDate'], 'validation.expectedBeforeOrder')
    if (!/^\d{1,3}$/.test(v.paymentTermDays.trim()) || Number(v.paymentTermDays) > 365) issue(['paymentTermDays'], 'validation.paymentTermDays')
    if (v.lines.length === 0) issue(['lines'], 'validation.linesRequired')
    v.lines.forEach((line, i) => {
      const qty = parseQuantity(line.quantity)
      if (qty === null || qty <= 0 || !hasAtMostThreeDecimals(qty)) issue(['lines', i, 'quantity'], 'validation.positiveQuantity')
      if (parsePrice(line.unitPrice, language) === null) issue(['lines', i, 'unitPrice'], 'validation.price')
      if (isOverOrdered(line) && !line.overOrderReason.trim()) issue(['lines', i, 'overOrderReason'], 'purchasing.orders.overOrderReasonRequired')
    })
  })
}

let nextKey = 1

/**
 * Adds picked request lines: one order line per material (the backend requires it), quantity raised
 * by what each picked request line still needs. Lines already on the order are skipped.
 */
export function addRequestLines(lines: OrderLineValues[], picked: OrderableLine[]): OrderLineValues[] {
  const result = lines.map((l) => ({ ...l, sources: [...l.sources] }))
  const used = new Set(result.flatMap((l) => l.sources.map((s) => s.requestLineId)))
  for (const p of picked) {
    if (used.has(p.requestLineId)) continue
    used.add(p.requestLineId)
    const source: OrderSource = {
      requestLineId: p.requestLineId,
      requestNumber: p.requestNumber,
      requestLineNo: p.lineNo,
      available: p.remainingQuantity,
    }
    const line = result.find((l) => l.material.id === p.materialId)
    if (line) {
      line.sources.push(source)
      line.quantity = String(Number(((parseQuantity(line.quantity) ?? 0) + p.remainingQuantity).toFixed(3)))
    } else {
      result.push({
        key: `new-${nextKey++}`,
        material: { id: p.materialId, code: p.materialCode, name: p.materialName, unitName: p.unitName },
        sources: [source],
        quantity: String(p.remainingQuantity),
        unitPrice: '',
        note: '',
        overOrderReason: '',
      })
    }
  }
  return result
}

export function newOrderValues(): OrderFormValues {
  return {
    supplier: null,
    orderDate: todayIso(),
    expectedDate: '',
    warehouseId: '',
    paymentTermDays: '0',
    vatRate: 'Ten',
    note: '',
    lines: [],
  }
}

/** Price for an editable box, grouped the way parsePrice reads it back. */
export function formatPriceInput(value: number, language: string): string {
  return new Intl.NumberFormat(language === 'vi' ? 'vi-VN' : 'en-US', { maximumFractionDigits: 2 }).format(value)
}

/**
 * Form values from a saved order. <paramref name="remaining"/>: what each source request line still
 * needs beyond this order (from the orderable lines), so editing knows how much more can be taken.
 */
export function orderToValues(order: PurchaseOrder, language: string, remaining: Map<string, number> = new Map()): OrderFormValues {
  return {
    supplier: { id: order.supplier.id, code: order.supplier.code, name: order.supplier.name, paymentTermDays: order.paymentTermDays },
    orderDate: order.orderDate,
    expectedDate: order.expectedDate ?? '',
    warehouseId: order.warehouseId,
    paymentTermDays: String(order.paymentTermDays),
    vatRate: order.vatRate,
    note: order.note ?? '',
    lines: order.lines.map((l) => ({
      key: l.id,
      material: { id: l.materialId, code: l.materialCode, name: l.materialName, unitName: l.unitName },
      sources: l.sources.map((s) => ({
        requestLineId: s.requestLineId,
        requestNumber: s.requestNumber,
        requestLineNo: s.requestLineNo,
        available: Number((s.quantity + (remaining.get(s.requestLineId) ?? 0)).toFixed(3)),
      })),
      quantity: formatQuantityInput(l.quantity, language),
      unitPrice: formatPriceInput(l.unitPrice, language),
      note: l.note ?? '',
      overOrderReason: l.overOrderReason ?? '',
    })),
  }
}

export function valuesToOrderInput(v: OrderFormValues, language: string): PurchaseOrderInput {
  return {
    supplierId: v.supplier!.id,
    orderDate: v.orderDate,
    expectedDate: v.expectedDate || null,
    warehouseId: v.warehouseId,
    paymentTermDays: Number(v.paymentTermDays),
    vatRate: v.vatRate,
    note: v.note.trim() || null,
    lines: v.lines.map((l) => ({
      materialId: l.material.id,
      quantity: parseQuantity(l.quantity)!,
      unitPrice: parsePrice(l.unitPrice, language)!,
      sourceLineIds: l.sources.map((s) => s.requestLineId),
      note: l.note.trim() || null,
      overOrderReason: isOverOrdered(l) ? l.overOrderReason.trim() || null : null,
    })),
  }
}
