import { describe, expect, it } from 'vitest'
import type { OrderableLine } from '../types'
import {
  addRequestLines,
  isOverOrdered,
  lineAmount,
  newOrderValues,
  orderSchema,
  orderTotals,
  parsePrice,
  valuesToOrderInput,
  type OrderFormValues,
} from './orderModel'

const orderable = (requestLineId: string, materialId: string, remaining: number, requestNumber = 'DN-2026-00001'): OrderableLine => ({
  requestLineId,
  requestId: `r-${requestNumber}`,
  requestNumber,
  departmentName: 'Sản xuất',
  requesterName: 'Nguyễn Văn Sản',
  neededDate: null,
  lineNo: 1,
  materialId,
  materialCode: materialId.toUpperCase(),
  materialName: `Vật tư ${materialId}`,
  unitName: 'Cái',
  quantity: remaining,
  orderedQuantity: 0,
  remainingQuantity: remaining,
  note: null,
})

describe('parsePrice', () => {
  it('reads grouped prices per language with up to 2 decimals', () => {
    expect(parsePrice('12.500', 'vi')).toBe(12500)
    expect(parsePrice('1.234,56', 'vi')).toBe(1234.56)
    expect(parsePrice('12,500.5', 'en')).toBe(12500.5)
    expect(parsePrice('0', 'vi')).toBe(0)
    expect(parsePrice('1,005', 'vi')).toBeNull() // 3 decimals
    expect(parsePrice('-5', 'vi')).toBeNull()
    expect(parsePrice('', 'vi')).toBeNull()
  })
})

describe('money', () => {
  it('rounds line amounts and VAT like the backend', () => {
    expect(lineAmount(12, 1234.56)).toBe(14815) // 14814.72
    expect(lineAmount(2.5, 10001)).toBe(25003) // 25002.5 away from zero
    expect(orderTotals([14815], 'Ten')).toEqual({ subtotal: 14815, vat: 1482, total: 16297 })
    expect(orderTotals([600000, 400005], 'Eight')).toEqual({ subtotal: 1000005, vat: 80000, total: 1080005 })
    expect(orderTotals([1000], 'Exempt').vat).toBe(0)
  })
})

describe('addRequestLines', () => {
  it('merges request lines of the same material into one order line and skips lines already on the order', () => {
    const first = addRequestLines([], [orderable('a', 'bolt', 10), orderable('b', 'oil', 4), orderable('c', 'bolt', 5, 'DN-2026-00002')])
    expect(first.map((l) => [l.material.id, l.quantity, l.sources.map((s) => s.requestLineId)])).toEqual([
      ['bolt', '15', ['a', 'c']],
      ['oil', '4', ['b']],
    ])
    const again = addRequestLines(first, [orderable('a', 'bolt', 10), orderable('d', 'oil', 1.5)])
    expect(again.map((l) => [l.material.id, l.quantity])).toEqual([
      ['bolt', '15'],
      ['oil', '5.5'],
    ])
    expect(first[1].quantity).toBe('4') // the input is not mutated
  })
})

describe('orderSchema', () => {
  const base = (patch: Partial<OrderFormValues> = {}): OrderFormValues => ({
    ...newOrderValues(),
    supplier: { id: 's', code: 'NCC', name: 'NCC', paymentTermDays: 30 },
    warehouseId: 'w',
    lines: addRequestLines([], [orderable('a', 'bolt', 10)]).map((l) => ({ ...l, unitPrice: '12.000' })),
    ...patch,
  })
  const errorsOf = (v: OrderFormValues) => {
    const result = orderSchema('vi').safeParse(v)
    return result.success ? [] : result.error.issues.map((i) => `${i.path.join('.')}:${i.message}`)
  }

  it('accepts a valid order and builds the request body', () => {
    const v = base()
    expect(errorsOf(v)).toEqual([])
    expect(valuesToOrderInput(v, 'vi').lines).toEqual([
      { materialId: 'bolt', quantity: 10, unitPrice: 12000, sourceLineIds: ['a'], note: null, overOrderReason: null },
    ])
  })

  it('asks for a reason only when ordering more than requested', () => {
    const over = base()
    over.lines[0].quantity = '12'
    expect(isOverOrdered(over.lines[0])).toBe(true)
    expect(errorsOf(over)).toEqual(['lines.0.overOrderReason:purchasing.orders.overOrderReasonRequired'])
    over.lines[0].overOrderReason = 'NCC bán theo thùng 12'
    expect(errorsOf(over)).toEqual([])
    expect(valuesToOrderInput(over, 'vi').lines[0].overOrderReason).toBe('NCC bán theo thùng 12')

    const under = base()
    under.lines[0].quantity = '8'
    under.lines[0].overOrderReason = 'left over from an earlier edit'
    expect(valuesToOrderInput(under, 'vi').lines[0].overOrderReason).toBeNull()
  })

  it('checks header and line fields', () => {
    const v = base({ supplier: null, expectedDate: '2000-01-01', paymentTermDays: '400' })
    v.lines[0].unitPrice = 'abc'
    v.lines[0].quantity = '0'
    expect(errorsOf(v)).toEqual([
      'supplier:validation.required',
      'expectedDate:validation.expectedBeforeOrder',
      'paymentTermDays:validation.paymentTermDays',
      'lines.0.quantity:validation.positiveQuantity',
      'lines.0.unitPrice:validation.price',
    ])
  })
})
