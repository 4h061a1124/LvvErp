import type { Role } from '@/lib/permissions'

// Mirrors backend UserDto / CreateUserRequest / UpdateUserRequest.

export interface User {
  id: string
  userName: string
  fullName: string
  email: string | null
  roles: Role[]
  isActive: boolean
  lastLoginAt: string | null
  createdAt: string
  employeeId: string | null
  employeeCode: string | null
  employeeName: string | null
  departmentName: string | null
}

export interface CreateUserInput {
  userName: string
  fullName: string
  email: string | null
  password: string
  roles: Role[]
  employeeId: string | null
}

/** <c>newPassword</c> null keeps the current password. */
export interface UpdateUserInput {
  fullName: string
  email: string | null
  roles: Role[]
  isActive: boolean
  newPassword: string | null
  employeeId: string | null
}
