import { z } from 'zod'
import { EMPLOYEE_STATUSES, type EmployeeInput } from './types'

// Same rules as the backend SaveEmployeeRequestValidator. Messages are i18n keys.
const optionalText = (max: number, pattern: RegExp, message: string) =>
  z
    .string()
    .trim()
    .max(max, 'validation.tooLong')
    .refine((v) => v === '' || pattern.test(v), message)

export const employeeSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, 'validation.required')
    .max(20, 'validation.tooLong')
    .regex(/^[A-Za-z0-9._-]+$/, 'validation.codeFormat'),
  fullName: z.string().trim().min(1, 'validation.required').max(200, 'validation.tooLong'),
  departmentId: z.string().min(1, 'validation.required'),
  positionId: z.string().min(1, 'validation.required'),
  email: optionalText(200, /^[^\s@]+@[^\s@]+\.[^\s@]+$/, 'validation.email'),
  phone: optionalText(20, /^[0-9+\-\s().]+$/, 'validation.phone'),
  status: z.enum(EMPLOYEE_STATUSES),
})

export type EmployeeFormValues = z.infer<typeof employeeSchema>

export function toEmployeeInput(values: EmployeeFormValues): EmployeeInput {
  return {
    ...values,
    code: values.code.toUpperCase(),
    email: values.email || null,
    phone: values.phone || null,
  }
}
