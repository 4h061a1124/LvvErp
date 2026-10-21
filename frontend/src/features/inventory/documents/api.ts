import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { PagedResult } from '@/lib/types'
import type {
  AvailableStock,
  DocumentInput,
  DocumentListParams,
  DocumentType,
  Lot,
  StockDocument,
  StockDocumentSummary,
} from './types'
import { DOCUMENT_PATHS } from './types'

export const documentKeys = {
  all: ['inventory', 'documents'] as const,
  list: (type: DocumentType, params: DocumentListParams) => ['inventory', 'documents', type, 'list', params] as const,
  detail: (id: string) => ['inventory', 'documents', 'detail', id] as const,
}

/** Everything that changes when stock moves (documents, balances, cards, warnings). */
export const stockKeys = {
  all: ['inventory', 'stock'] as const,
}

export function useDocuments(type: DocumentType, params: DocumentListParams) {
  return useQuery({
    queryKey: documentKeys.list(type, params),
    queryFn: async () =>
      (await api.get<PagedResult<StockDocumentSummary>>(DOCUMENT_PATHS[type].api, { params })).data,
    placeholderData: keepPreviousData,
  })
}

export function useDocument(type: DocumentType, id: string | undefined) {
  return useQuery({
    queryKey: documentKeys.detail(id ?? ''),
    queryFn: async () => (await api.get<StockDocument>(`${DOCUMENT_PATHS[type].api}/${id}`)).data,
    enabled: !!id,
  })
}

function useInvalidateStock() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: documentKeys.all }),
      queryClient.invalidateQueries({ queryKey: stockKeys.all }),
    ])
}

export function useSaveDocument(type: DocumentType) {
  const invalidate = useInvalidateStock()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: DocumentInput }) => {
      const path = DOCUMENT_PATHS[type].api
      return id
        ? (await api.put<StockDocument>(`${path}/${id}`, input)).data
        : (await api.post<StockDocument>(path, input)).data
    },
    onSuccess: invalidate,
  })
}

export function useDocumentAction(type: DocumentType) {
  const invalidate = useInvalidateStock()
  const path = DOCUMENT_PATHS[type].api
  return {
    approve: useMutation({
      mutationFn: async (id: string) => (await api.post<StockDocument>(`${path}/${id}/approve`)).data,
      onSuccess: invalidate,
    }),
    cancel: useMutation({
      mutationFn: async ({ id, reason }: { id: string; reason: string }) =>
        (await api.post<StockDocument>(`${path}/${id}/cancel`, { reason })).data,
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.delete(`${path}/${id}`),
      onSuccess: invalidate,
    }),
  }
}

/** Stock of one material in one warehouse by location and lot, earliest expiry first. */
export function useAvailableStock(materialId: string | undefined, warehouseId: string | undefined) {
  return useQuery({
    queryKey: [...stockKeys.all, 'available', materialId, warehouseId],
    queryFn: async () =>
      (await api.get<AvailableStock[]>('/stock/available', { params: { materialId, warehouseId } })).data,
    enabled: !!materialId && !!warehouseId,
  })
}

export function useLots(materialId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: [...stockKeys.all, 'lots', materialId],
    queryFn: async () => (await api.get<Lot[]>(`/materials/${materialId}/lots`)).data,
    enabled: !!materialId && enabled,
  })
}
