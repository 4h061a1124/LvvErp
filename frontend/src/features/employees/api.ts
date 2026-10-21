import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { Employee, EmployeeInput, EmployeeListParams, Lookup, PagedResult } from './types'

export const employeeKeys = {
  all: ['employees'] as const,
  list: (params: EmployeeListParams) => ['employees', 'list', params] as const,
}

export function useEmployees(params: EmployeeListParams) {
  return useQuery({
    queryKey: employeeKeys.list(params),
    queryFn: async () => (await api.get<PagedResult<Employee>>('/employees', { params })).data,
    placeholderData: keepPreviousData,
  })
}

export function useDepartments() {
  return useQuery({
    queryKey: ['departments'],
    queryFn: async () => (await api.get<Lookup[]>('/departments')).data,
    staleTime: 10 * 60_000,
  })
}

export function usePositions() {
  return useQuery({
    queryKey: ['positions'],
    queryFn: async () => (await api.get<Lookup[]>('/positions')).data,
    staleTime: 10 * 60_000,
  })
}

export function useSaveEmployee() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: EmployeeInput }) =>
      id
        ? (await api.put<Employee>(`/employees/${id}`, input)).data
        : (await api.post<Employee>('/employees', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: employeeKeys.all }),
  })
}

export function useDeleteEmployee() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/employees/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: employeeKeys.all }),
  })
}

export async function uploadPhoto(id: string, file: File) {
  const form = new FormData()
  form.append('file', file)
  return (await api.post<Employee>(`/employees/${id}/photo`, form)).data
}

export async function deletePhoto(id: string) {
  await api.delete(`/employees/${id}/photo`)
}

