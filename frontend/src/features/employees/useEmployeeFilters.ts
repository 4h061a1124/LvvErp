import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'
import { EMPLOYEE_STATUSES, type EmployeeListParams, type EmployeeStatus } from './types'

export interface EmployeeFilters {
  search: string
  departmentId: string
  status: EmployeeStatus | ''
  page: number
  pageSize: number
  sort: string
}

const DEFAULTS: EmployeeFilters = { search: '', departmentId: '', status: '', page: 1, pageSize: 20, sort: 'code' }

// Short URL names so a filtered list can be bookmarked or shared: ?q=&dept=&status=&page=&size=&sort=
const PARAM: Record<keyof EmployeeFilters, string> = {
  search: 'q',
  departmentId: 'dept',
  status: 'status',
  page: 'page',
  pageSize: 'size',
  sort: 'sort',
}

export function useEmployeeFilters() {
  const [params, setParams] = useSearchParams()

  const filters = useMemo<EmployeeFilters>(() => {
    const status = params.get(PARAM.status) as EmployeeStatus | null
    return {
      search: params.get(PARAM.search) ?? DEFAULTS.search,
      departmentId: params.get(PARAM.departmentId) ?? DEFAULTS.departmentId,
      status: status && EMPLOYEE_STATUSES.includes(status) ? status : '',
      page: Math.max(1, Number(params.get(PARAM.page)) || DEFAULTS.page),
      pageSize: Number(params.get(PARAM.pageSize)) || DEFAULTS.pageSize,
      sort: params.get(PARAM.sort) ?? DEFAULTS.sort,
    }
  }, [params])

  /** Any change other than paging sends the user back to page 1. */
  const setFilters = useCallback(
    (patch: Partial<EmployeeFilters>) => {
      setParams(
        (prev) => {
          const next = new URLSearchParams(prev)
          const merged = { ...filters, ...patch }
          if (!('page' in patch)) merged.page = 1
          for (const key of Object.keys(PARAM) as (keyof EmployeeFilters)[]) {
            const value = merged[key]
            if (value === DEFAULTS[key] || value === '') next.delete(PARAM[key])
            else next.set(PARAM[key], String(value))
          }
          return next
        },
        { replace: true },
      )
    },
    [filters, setParams],
  )

  const queryParams: EmployeeListParams = {
    search: filters.search || undefined,
    departmentId: filters.departmentId || undefined,
    status: filters.status || undefined,
    page: filters.page,
    pageSize: filters.pageSize,
    sort: filters.sort,
  }

  const hasFilters = !!(filters.search || filters.departmentId || filters.status)

  return { filters, setFilters, queryParams, hasFilters }
}
