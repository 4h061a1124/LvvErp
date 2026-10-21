export const EMPLOYEE_STATUSES = ['Active', 'OnLeave', 'Resigned'] as const
export type EmployeeStatus = (typeof EMPLOYEE_STATUSES)[number]

export interface Employee {
  id: string
  code: string
  fullName: string
  departmentId: string
  departmentName: string
  positionId: string
  positionName: string
  email: string | null
  phone: string | null
  photoUrl: string | null
  status: EmployeeStatus
  createdAt: string
  updatedAt: string | null
}

export interface EmployeeInput {
  code: string
  fullName: string
  departmentId: string
  positionId: string
  email: string | null
  phone: string | null
  status: EmployeeStatus
}

export interface Lookup {
  id: string
  code: string
  name: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface EmployeeListParams {
  search?: string
  departmentId?: string
  status?: EmployeeStatus
  page: number
  pageSize: number
  sort?: string
}

export interface ImportError {
  row: number
  column: string
  errorCode: string
  message: string
  value: string | null
}

export interface ImportResult {
  succeeded: boolean
  totalRows: number
  created: number
  updated: number
  errors: ImportError[]
}
