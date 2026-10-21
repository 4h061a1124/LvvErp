import { z } from 'zod'
import { hasAtMostThreeDecimals, parseQuantity } from '@/lib/format'
import type { MaterialInput } from './types'

// Same rules as the backend validators. Messages are i18n keys.
const code = (max: number) =>
  z
    .string()
    .trim()
    .min(1, 'validation.required')
    .max(max, 'validation.tooLong')
    .regex(/^[A-Za-z0-9._-]+$/, 'validation.codeFormat')

export const catalogItemSchema = z.object({
  code: code(20),
  name: z.string().trim().min(1, 'validation.required').max(100, 'validation.tooLong'),
  isActive: z.boolean(),
})
export type CatalogItemFormValues = z.infer<typeof catalogItemSchema>

export const warehouseSchema = catalogItemSchema.extend({
  note: z.string().trim().max(500, 'validation.tooLong'),
})
export type WarehouseFormValues = z.infer<typeof warehouseSchema>

export const locationSchema = z.object({
  code: code(30),
  description: z.string().trim().max(200, 'validation.tooLong'),
  isActive: z.boolean(),
})
export type LocationFormValues = z.infer<typeof locationSchema>

/** Typed as text so "12,5" works; must be a number ≥ 0 with at most 3 decimals. */
export const quantityText = z.string().refine((v) => {
  const n = parseQuantity(v)
  return n !== null && n >= 0 && hasAtMostThreeDecimals(n)
}, 'validation.quantity')

export const materialSchema = z.object({
  code: code(30),
  name: z.string().trim().min(1, 'validation.required').max(200, 'validation.tooLong'),
  specification: z.string().trim().max(500, 'validation.tooLong'),
  unitId: z.string().min(1, 'validation.required'),
  groupId: z.string().min(1, 'validation.required'),
  minStock: quantityText,
  trackLots: z.boolean(),
  isActive: z.boolean(),
})
export type MaterialFormValues = z.infer<typeof materialSchema>

export function toMaterialInput(values: MaterialFormValues): MaterialInput {
  return {
    ...values,
    code: values.code.toUpperCase(),
    specification: values.specification || null,
    minStock: parseQuantity(values.minStock) ?? 0,
  }
}
