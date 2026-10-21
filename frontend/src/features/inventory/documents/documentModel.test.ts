import { describe, expect, it } from 'vitest'
import { todayIso } from '@/lib/format'
import {
  documentSchema,
  documentToValues,
  emptyLine,
  newDocumentValues,
  parseSourceKey,
  sourceKey,
  valuesToInput,
  type DocumentFormValues,
  type MaterialRef,
} from './documentModel'
import type { StockDocument } from './types'

const bolt: MaterialRef = { id: 'm1', code: 'BL-M8', name: 'Bu lông', unitName: 'Cái', trackLots: false }
const paint: MaterialRef = { id: 'm2', code: 'SON', name: 'Sơn', unitName: 'Thùng', trackLots: true }

function values(type: 'Receipt' | 'Issue' | 'Transfer', patch: Partial<DocumentFormValues> = {}): DocumentFormValues {
  return { ...newDocumentValues(type), warehouseId: 'w1', ...patch }
}

const errorsOf = (type: 'Receipt' | 'Issue' | 'Transfer', v: DocumentFormValues) => {
  const result = documentSchema(type).safeParse(v)
  return result.success ? [] : result.error.issues.map((i) => `${i.path.join('.')}:${i.message}`)
}

describe('sourceKey', () => {
  it('round-trips location and lot, including missing parts', () => {
    expect(parseSourceKey(sourceKey('loc', 'lot'))).toEqual({ locationId: 'loc', lotId: 'lot' })
    expect(parseSourceKey(sourceKey(null, 'lot'))).toEqual({ locationId: null, lotId: 'lot' })
    expect(parseSourceKey(sourceKey(null, null))).toEqual({ locationId: null, lotId: null })
  })
})

describe('documentSchema', () => {
  it('accepts a simple receipt and defaults the reason', () => {
    const v = values('Receipt', { lines: [{ ...emptyLine(bolt), quantity: '10,5' }] })
    expect(v.reason).toBe('Purchase')
    expect(errorsOf('Receipt', v)).toEqual([])
  })

  it('requires a lot number for lot-tracked materials on receipts', () => {
    const v = values('Receipt', { lines: [{ ...emptyLine(paint), quantity: '1' }] })
    expect(errorsOf('Receipt', v)).toEqual(['lines.0.lotNumber:inventory.documents.lotRequired'])
  })

  it('rejects empty material, zero quantity, expiry before manufacture and future dates', () => {
    const v = values('Receipt', {
      documentDate: '2999-01-01',
      lines: [
        { ...emptyLine(), quantity: '1' },
        { ...emptyLine(bolt), quantity: '0' },
        { ...emptyLine(paint), quantity: '1', lotNumber: 'L1', manufactureDate: '2026-05-01', expiryDate: '2026-04-01' },
      ],
    })
    expect(errorsOf('Receipt', v)).toEqual([
      'documentDate:validation.futureDate',
      'lines.0.material:validation.required',
      'lines.1.quantity:validation.positiveQuantity',
      'lines.2.expiryDate:validation.expiryBeforeManufacture',
    ])
  })

  it('needs a stock source on issues', () => {
    const v = values('Issue', { lines: [{ ...emptyLine(bolt), quantity: '2' }] })
    expect(errorsOf('Issue', v)).toEqual(['lines.0.source:inventory.documents.sourceRequired'])
  })

  it('needs a destination on transfers that differs from the source', () => {
    const missing = values('Transfer', { lines: [] })
    expect(errorsOf('Transfer', missing)).toEqual(['toWarehouseId:validation.required'])

    const same = values('Transfer', {
      toWarehouseId: 'w1',
      lines: [{ ...emptyLine(bolt), quantity: '1', source: sourceKey('A-01', null), toLocationId: 'A-01' }],
    })
    expect(errorsOf('Transfer', same)).toEqual(['lines.0.toLocationId:inventory.documents.sameAsSource'])

    const otherShelf = { ...same, lines: [{ ...same.lines[0], toLocationId: 'B-01' }] }
    expect(errorsOf('Transfer', otherShelf)).toEqual([])
  })

  it('allows today', () => {
    expect(errorsOf('Receipt', values('Receipt', { documentDate: todayIso() }))).toEqual([])
  })
})

describe('valuesToInput', () => {
  it('sends lot fields only for lot-tracked receipt lines', () => {
    const input = valuesToInput(
      'Receipt',
      values('Receipt', {
        partner: 'Công ty A',
        lines: [
          { ...emptyLine(bolt), quantity: '1.000', lotNumber: 'ignored', locationId: 'loc' },
          { ...emptyLine(paint), quantity: '2,5', lotNumber: ' L1 ', expiryDate: '2027-01-31' },
        ],
      }),
    )
    expect(input).toMatchObject({ warehouseId: 'w1', reason: 'Purchase', partner: 'Công ty A', note: null })
    expect(input.lines[0]).toEqual({ materialId: 'm1', locationId: 'loc', lotNumber: null, manufactureDate: null, expiryDate: null, quantity: 1 })
    expect(input.lines[1]).toMatchObject({ materialId: 'm2', lotNumber: 'L1', expiryDate: '2027-01-31', quantity: 2.5 })
  })

  it('splits the chosen source into location and lot for issues and transfers', () => {
    const issue = valuesToInput(
      'Issue',
      values('Issue', {
        employee: { id: 'e1', code: 'NV1', fullName: 'A', departmentId: 'd1' },
        departmentId: 'd1',
        lines: [{ ...emptyLine(paint), quantity: '3', source: sourceKey('loc', 'lot') }],
      }),
    )
    expect(issue).toMatchObject({ employeeId: 'e1', departmentId: 'd1', reason: 'Production' })
    expect(issue.lines[0]).toEqual({ materialId: 'm2', locationId: 'loc', lotId: 'lot', quantity: 3 })

    const transfer = valuesToInput(
      'Transfer',
      values('Transfer', { toWarehouseId: 'w2', lines: [{ ...emptyLine(bolt), quantity: '4', source: sourceKey(null, null), toLocationId: 'B-01' }] }),
    )
    expect(transfer).toMatchObject({ warehouseId: 'w1', toWarehouseId: 'w2' })
    expect(transfer).not.toHaveProperty('reason')
    expect(transfer.lines[0]).toEqual({ materialId: 'm1', locationId: null, lotId: null, toLocationId: 'B-01', quantity: 4 })
  })
})

describe('documentToValues', () => {
  it('turns an issue line back into its source key', () => {
    const doc = {
      type: 'Issue',
      documentDate: '2026-10-01',
      warehouseId: 'w1',
      toWarehouseId: null,
      reason: 'Consumption',
      partner: null,
      departmentId: 'd1',
      employeeId: 'e1',
      employeeName: 'Trần Văn A',
      note: null,
      lines: [
        {
          id: 'l1', lineNo: 1, materialId: 'm2', materialCode: 'SON', materialName: 'Sơn', unitName: 'Thùng', trackLots: true,
          locationId: 'loc', locationCode: 'A-01', toLocationId: null, toLocationCode: null, lotId: 'lot', lotNumber: 'L1',
          manufactureDate: null, expiryDate: '2027-01-01', quantity: 2.5, note: null,
        },
      ],
    } as unknown as StockDocument

    const v = documentToValues(doc)

    expect(v.lines[0]).toMatchObject({ key: 'l1', source: sourceKey('loc', 'lot'), lotNumber: '', quantity: '2.5' })
    expect(v.employee).toMatchObject({ id: 'e1', fullName: 'Trần Văn A' })
    expect(valuesToInput('Issue', v).lines[0]).toMatchObject({ locationId: 'loc', lotId: 'lot', quantity: 2.5 })
  })
})
