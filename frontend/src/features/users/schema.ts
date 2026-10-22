import { z } from 'zod'
import type { EmployeeRef } from '@/features/inventory/documents/pickers'
import { ROLES, type Role } from '@/lib/permissions'
import type { CreateUserInput, UpdateUserInput, User } from './types'

// Same rules as the backend Create/UpdateUserRequestValidator. Messages are i18n keys.

/** At least 8 characters with a letter and a digit (ValidationRules.StrongPassword). */
export function isStrongPassword(value: string): boolean {
  return value.length >= 8 && value.length <= 128 && /[A-Za-z]/.test(value) && /[0-9]/.test(value)
}

export const userSchema = z
  .object({
    /** Only checked when creating; the username can't change later. */
    isNew: z.boolean(),
    userName: z.string().trim(),
    fullName: z.string().trim().min(1, 'validation.required').max(200, 'validation.tooLong'),
    email: z
      .string()
      .trim()
      .max(200, 'validation.tooLong')
      .refine((v) => v === '' || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v), 'validation.email'),
    /** Required when creating; optional reset when editing. */
    password: z.string(),
    roles: z.array(z.enum(ROLES)).min(1, 'users.form.rolesRequired'),
    employee: z.custom<EmployeeRef | null>(),
    isActive: z.boolean(),
  })
  .superRefine((v, ctx) => {
    const issue = (path: string, message: string) => ctx.addIssue({ code: 'custom', path: [path], message })
    if (v.isNew) {
      if (!v.userName) issue('userName', 'validation.required')
      else if (v.userName.length < 3 || v.userName.length > 50) issue('userName', 'validation.userNameLength')
      else if (!/^[A-Za-z0-9._-]+$/.test(v.userName)) issue('userName', 'validation.codeFormat')
    }
    if ((v.isNew || v.password !== '') && !isStrongPassword(v.password)) issue('password', 'auth.passwordRule')
  })
export type UserFormValues = z.infer<typeof userSchema>

export function newUserValues(): UserFormValues {
  return { isNew: true, userName: '', fullName: '', email: '', password: '', roles: ['Employee'], employee: null, isActive: true }
}

export function userToValues(user: User): UserFormValues {
  return {
    isNew: false,
    userName: user.userName,
    fullName: user.fullName,
    email: user.email ?? '',
    password: '',
    roles: user.roles,
    // The account list carries the employee's code and name but not the department id; the picker only shows the name.
    employee: user.employeeId
      ? { id: user.employeeId, code: user.employeeCode ?? '', fullName: user.employeeName ?? '', departmentId: '' }
      : null,
    isActive: user.isActive,
  }
}

/** Roles in the fixed display order, without duplicates. */
function orderedRoles(roles: Role[]): Role[] {
  return ROLES.filter((r) => roles.includes(r))
}

export function toCreateInput(v: UserFormValues): CreateUserInput {
  return {
    userName: v.userName.trim().toLowerCase(),
    fullName: v.fullName.trim(),
    email: v.email.trim() || null,
    password: v.password,
    roles: orderedRoles(v.roles),
    employeeId: v.employee?.id ?? null,
  }
}

export function toUpdateInput(v: UserFormValues): UpdateUserInput {
  return {
    fullName: v.fullName.trim(),
    email: v.email.trim() || null,
    roles: orderedRoles(v.roles),
    isActive: v.isActive,
    newPassword: v.password === '' ? null : v.password,
    employeeId: v.employee?.id ?? null,
  }
}
