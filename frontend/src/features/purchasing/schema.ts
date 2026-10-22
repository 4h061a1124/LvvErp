import { z } from 'zod'
import type { SupplierInput } from './types'

// Same rules as the backend validators. Messages are i18n keys.

const optional = (max: number) => z.string().trim().max(max, 'validation.tooLong')
const optionalPattern = (max: number, pattern: RegExp, message: string) =>
  optional(max).refine((v) => v === '' || pattern.test(v), message)

export const supplierSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, 'validation.required')
    .max(20, 'validation.tooLong')
    .regex(/^[A-Za-z0-9._-]+$/, 'validation.codeFormat'),
  name: z.string().trim().min(1, 'validation.required').max(200, 'validation.tooLong'),
  taxCode: optionalPattern(14, /^\d{10}(-\d{3})?$/, 'validation.taxCode'),
  address: optional(500),
  contactName: optional(100),
  phone: optionalPattern(20, /^[0-9+\-\s().]+$/, 'validation.phone'),
  email: optionalPattern(200, /^[^\s@]+@[^\s@]+\.[^\s@]+$/, 'validation.email'),
  paymentTermDays: z.string().trim().refine((v) => /^\d{1,3}$/.test(v) && Number(v) <= 365, 'validation.paymentTermDays'),
  paymentTerms: optional(200),
  isActive: z.boolean(),
})
export type SupplierFormValues = z.infer<typeof supplierSchema>

export function toSupplierInput(values: SupplierFormValues): SupplierInput {
  const orNull = (v: string) => v.trim() || null
  return {
    code: values.code.trim().toUpperCase(),
    name: values.name.trim(),
    taxCode: orNull(values.taxCode),
    address: orNull(values.address),
    contactName: orNull(values.contactName),
    phone: orNull(values.phone),
    email: orNull(values.email),
    paymentTermDays: Number(values.paymentTermDays),
    paymentTerms: orNull(values.paymentTerms),
    isActive: values.isActive,
  }
}

/** Whole đồng typed with or without grouping ("50.000.000", "50,000,000", "50000000"). Null when invalid. */
export function parseMoney(text: string): number | null {
  const digits = text.trim().replace(/[.,\s]/g, '')
  if (!/^\d{1,15}$/.test(digits)) return null
  return Number(digits)
}
