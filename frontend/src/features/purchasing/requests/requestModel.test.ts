import { describe, expect, it } from 'vitest'
import type { MaterialRef } from '@/features/inventory/documents/documentModel'
import type { PurchaseRequest } from '../types'
import {
  addMaterial,
  emptyRequestLine,
  newRequestValues,
  requestSchema,
  requestToValues,
  valuesToRequestInput,
  type RequestFormValues,
} from './requestModel'

const bolt: MaterialRef = { id: 'm1', code: 'BL-M8', name: 'Bu lông', unitName: 'Cái', trackLots: false }
const oil: MaterialRef = { id: 'm2', code: 'DAU', name: 'Dầu thủy lực', unitName: 'Lít', trackLots: true }

const errorsOf = (v: RequestFormValues) => {
  const result = requestSchema.safeParse(v)
  return result.success ? [] : result.error.issues.map((i) => `${i.path.join('.')}:${i.message}`)
}

describe('requestSchema', () => {
  it('accepts a request with decimal-comma quantities', () => {
    const v = { ...newRequestValues(), lines: [{ ...emptyRequestLine(bolt), quantity: '12,5' }] }
    expect(errorsOf(v)).toEqual([])
    expect(valuesToRequestInput(v).lines).toEqual([{ materialId: 'm1', quantity: 12.5, note: null }])
  })

  it('needs at least one line, a material and a positive quantity with at most 3 decimals', () => {
    expect(errorsOf(newRequestValues())).toEqual(['lines:validation.linesRequired'])
    expect(
      errorsOf({
        ...newRequestValues(),
        lines: [emptyRequestLine(), { ...emptyRequestLine(oil), quantity: '0' }, { ...emptyRequestLine(bolt), quantity: '1,0001' }],
      }),
    ).toEqual(['lines.0.material:validation.required', 'lines.1.quantity:validation.positiveQuantity', 'lines.2.quantity:validation.positiveQuantity'])
  })

  it('rejects the same material twice and a needed date before the request date', () => {
    const v: RequestFormValues = {
      requestDate: '2026-10-21',
      neededDate: '2026-10-20',
      note: '',
      lines: [emptyRequestLine(bolt), emptyRequestLine(bolt)],
    }
    expect(errorsOf(v)).toEqual(['neededDate:validation.neededBeforeRequest', 'lines.1.material:purchasing.requests.duplicateMaterial'])
  })
})

describe('addMaterial', () => {
  it('bumps the quantity of a material already listed instead of adding a line', () => {
    const first = addMaterial([], bolt)
    expect(first.lines.map((l) => [l.material?.id, l.quantity])).toEqual([['m1', '1']])
    const second = addMaterial(first.lines, oil)
    const third = addMaterial(second.lines, bolt)
    expect(third.index).toBe(0)
    expect(third.lines.map((l) => [l.material?.id, l.quantity])).toEqual([
      ['m1', '2'],
      ['m2', '1'],
    ])
  })
})

describe('requestToValues', () => {
  it('fills the form from a saved request with local decimal separators', () => {
    const request = {
      requestDate: '2026-10-21',
      neededDate: null,
      note: 'Bảo trì máy ép',
      lines: [{ id: 'l1', materialId: 'm1', materialCode: 'BL-M8', materialName: 'Bu lông', unitName: 'Cái', quantity: 2.5, note: null }],
    } as unknown as PurchaseRequest
    const v = requestToValues(request, 'vi')
    expect(v.neededDate).toBe('')
    expect(v.lines[0].quantity).toBe('2,5')
    expect(valuesToRequestInput(v)).toEqual({
      requestDate: '2026-10-21',
      neededDate: null,
      note: 'Bảo trì máy ép',
      lines: [{ materialId: 'm1', quantity: 2.5, note: null }],
    })
  })
})
