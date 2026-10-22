import { z } from 'zod'
import type { MaterialRef } from '@/features/inventory/documents/documentModel'
import { formatQuantityInput, hasAtMostThreeDecimals, parseQuantity, todayIso } from '@/lib/format'
import type { PurchaseRequest, PurchaseRequestInput } from '../types'

const lineSchema = z.object({
  key: z.string(),
  material: z.custom<MaterialRef | null>(),
  quantity: z.string(),
  note: z.string().trim().max(200, 'validation.tooLong'),
})
export type RequestLineValues = z.infer<typeof lineSchema>

/** Same rules as the backend SavePurchaseRequestRequestValidator; messages are i18n keys. */
export const requestSchema = z
  .object({
    requestDate: z.string().min(1, 'validation.required'),
    neededDate: z.string(),
    note: z.string().trim().max(500, 'validation.tooLong'),
    lines: z.array(lineSchema),
  })
  .superRefine((v, ctx) => {
    const issue = (path: (string | number)[], message: string) => ctx.addIssue({ code: 'custom', path, message })

    if (v.neededDate && v.requestDate && v.neededDate < v.requestDate) issue(['neededDate'], 'validation.neededBeforeRequest')
    if (v.lines.length === 0) issue(['lines'], 'validation.linesRequired')

    const seen = new Set<string>()
    v.lines.forEach((line, i) => {
      if (!line.material) return issue(['lines', i, 'material'], 'validation.required')
      if (seen.has(line.material.id)) issue(['lines', i, 'material'], 'purchasing.requests.duplicateMaterial')
      seen.add(line.material.id)
      const qty = parseQuantity(line.quantity)
      if (qty === null || qty <= 0 || !hasAtMostThreeDecimals(qty)) issue(['lines', i, 'quantity'], 'validation.positiveQuantity')
    })
  })
export type RequestFormValues = z.infer<typeof requestSchema>

let nextKey = 1
export function emptyRequestLine(material: MaterialRef | null = null): RequestLineValues {
  return { key: `new-${nextKey++}`, material, quantity: material ? '1' : '', note: '' }
}

export function newRequestValues(): RequestFormValues {
  return { requestDate: todayIso(), neededDate: '', note: '', lines: [] }
}

export function requestToValues(request: PurchaseRequest, language: string): RequestFormValues {
  return {
    requestDate: request.requestDate,
    neededDate: request.neededDate ?? '',
    note: request.note ?? '',
    lines: request.lines.map((l) => ({
      key: l.id,
      // Lot tracking does not matter for a request; the picker only needs to show the material.
      material: { id: l.materialId, code: l.materialCode, name: l.materialName, unitName: l.unitName, trackLots: false },
      quantity: formatQuantityInput(l.quantity, language),
      note: l.note ?? '',
    })),
  }
}

export function valuesToRequestInput(values: RequestFormValues): PurchaseRequestInput {
  return {
    requestDate: values.requestDate,
    neededDate: values.neededDate || null,
    note: values.note.trim() || null,
    lines: values.lines.map((l) => ({
      materialId: l.material!.id,
      quantity: parseQuantity(l.quantity)!,
      note: l.note.trim() || null,
    })),
  }
}

/**
 * Adds a scanned or picked material: bumps the quantity of a line that already has it
 * (a request lists each material once), otherwise appends a line.
 */
export function addMaterial(lines: RequestLineValues[], material: MaterialRef): { lines: RequestLineValues[]; index: number } {
  const index = lines.findIndex((l) => l.material?.id === material.id)
  if (index < 0) return { lines: [...lines, emptyRequestLine(material)], index: lines.length }
  const current = parseQuantity(lines[index].quantity) ?? 0
  const bumped = { ...lines[index], quantity: String(current + 1) }
  return { lines: lines.map((l, i) => (i === index ? bumped : l)), index }
}
