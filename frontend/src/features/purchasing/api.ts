import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { PagedResult } from '@/lib/types'
import type {
  ApprovalInbox,
  LastPrice,
  OrderableLine,
  OrderListParams,
  OverdueLine,
  PriceHistoryRow,
  PurchaseOrder,
  PurchaseOrderInput,
  PurchaseOrderSummary,
  PurchasingSummary,
  PurchaseRequest,
  PurchaseRequestInput,
  PurchaseRequestSummary,
  PurchasingSettings,
  RequestListParams,
  Supplier,
  SupplierInput,
  SupplierListParams,
} from './types'

export const purchasingKeys = {
  all: ['purchasing'] as const,
  suppliers: ['purchasing', 'suppliers'] as const,
  supplierList: (params: SupplierListParams) => ['purchasing', 'suppliers', 'list', params] as const,
  settings: ['purchasing', 'settings'] as const,
  /** Requests, orders and the approval inbox change together (ordering updates request status). */
  documents: ['purchasing', 'documents'] as const,
  requestList: (params: RequestListParams) => ['purchasing', 'documents', 'requests', 'list', params] as const,
  request: (id: string) => ['purchasing', 'documents', 'requests', 'detail', id] as const,
  inbox: ['purchasing', 'documents', 'inbox'] as const,
  orderList: (params: OrderListParams) => ['purchasing', 'documents', 'orders', 'list', params] as const,
  order: (id: string) => ['purchasing', 'documents', 'orders', 'detail', id] as const,
  orderable: (search: string) => ['purchasing', 'documents', 'orderable', search] as const,
  reports: ['purchasing', 'documents', 'reports'] as const,
}

// --- Suppliers ----------------------------------------------------------------------------

export function useSuppliers(params: SupplierListParams) {
  return useQuery({
    queryKey: purchasingKeys.supplierList(params),
    queryFn: async () => (await api.get<PagedResult<Supplier>>('/suppliers', { params })).data,
    placeholderData: keepPreviousData,
  })
}

export function useSaveSupplier() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: SupplierInput }) =>
      id ? (await api.put<Supplier>(`/suppliers/${id}`, input)).data : (await api.post<Supplier>('/suppliers', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: purchasingKeys.suppliers }),
  })
}

export function useDeleteSupplier() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/suppliers/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: purchasingKeys.suppliers }),
  })
}

// --- Settings -----------------------------------------------------------------------------

export function usePurchasingSettings(enabled = true) {
  return useQuery({
    queryKey: purchasingKeys.settings,
    queryFn: async () => (await api.get<PurchasingSettings>('/purchasing/settings')).data,
    enabled,
  })
}

export function useSavePurchasingSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: {
      directorApprovalThreshold: number
      purchasingDepartmentId: string
      companyName: string | null
      companyAddress: string | null
      companyTaxCode: string | null
      companyPhone: string | null
    }) =>
      (await api.put<PurchasingSettings>('/purchasing/settings', input)).data,
    onSuccess: (data) => queryClient.setQueryData(purchasingKeys.settings, data),
  })
}

// --- Purchase requests --------------------------------------------------------------------

export function useRequests(params: RequestListParams) {
  return useQuery({
    queryKey: purchasingKeys.requestList(params),
    queryFn: async () => (await api.get<PagedResult<PurchaseRequestSummary>>('/purchase-requests', { params })).data,
    placeholderData: keepPreviousData,
  })
}

export function useRequest(id: string | undefined) {
  return useQuery({
    queryKey: purchasingKeys.request(id ?? ''),
    queryFn: async () => (await api.get<PurchaseRequest>(`/purchase-requests/${id}`)).data,
    enabled: !!id,
  })
}

function useInvalidateDocuments() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: purchasingKeys.documents })
}

export function useSaveRequest() {
  const invalidate = useInvalidateDocuments()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: PurchaseRequestInput }) =>
      id
        ? (await api.put<PurchaseRequest>(`/purchase-requests/${id}`, input)).data
        : (await api.post<PurchaseRequest>('/purchase-requests', input)).data,
    onSuccess: invalidate,
  })
}

export type RequestAction = 'submit' | 'approve' | 'reject' | 'cancel' | 'close'

export function useRequestAction() {
  const invalidate = useInvalidateDocuments()
  return {
    run: useMutation({
      mutationFn: async ({ id, action, reason }: { id: string; action: RequestAction; reason?: string }) =>
        (await api.post<PurchaseRequest>(`/purchase-requests/${id}/${action}`, reason === undefined ? undefined : { reason })).data,
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.delete(`/purchase-requests/${id}`),
      onSuccess: invalidate,
    }),
  }
}

// --- Approval inbox -----------------------------------------------------------------------

export function useApprovalInbox(enabled = true) {
  return useQuery({
    queryKey: purchasingKeys.inbox,
    queryFn: async () => (await api.get<ApprovalInbox>('/purchasing/approvals')).data,
    enabled,
    refetchInterval: 60_000,
  })
}

// --- Purchase orders ----------------------------------------------------------------------

export function useOrders(params: OrderListParams) {
  return useQuery({
    queryKey: purchasingKeys.orderList(params),
    queryFn: async () => (await api.get<PagedResult<PurchaseOrderSummary>>('/purchase-orders', { params })).data,
    placeholderData: keepPreviousData,
  })
}

export function useOrder(id: string | undefined) {
  return useQuery({
    queryKey: purchasingKeys.order(id ?? ''),
    queryFn: async () => (await api.get<PurchaseOrder>(`/purchase-orders/${id}`)).data,
    enabled: !!id,
  })
}

export function useSaveOrder() {
  const invalidate = useInvalidateDocuments()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: PurchaseOrderInput }) =>
      id
        ? (await api.put<PurchaseOrder>(`/purchase-orders/${id}`, input)).data
        : (await api.post<PurchaseOrder>('/purchase-orders', input)).data,
    onSuccess: invalidate,
  })
}

export type OrderAction = 'submit' | 'approve' | 'reject' | 'cancel' | 'close'

export function useOrderAction() {
  const invalidate = useInvalidateDocuments()
  return {
    run: useMutation({
      mutationFn: async ({ id, action, reason }: { id: string; action: OrderAction; reason?: string }) =>
        (await api.post<PurchaseOrder>(`/purchase-orders/${id}/${action}`, reason === undefined ? undefined : { reason })).data,
      onSuccess: invalidate,
    }),
    remove: useMutation({
      mutationFn: (id: string) => api.delete(`/purchase-orders/${id}`),
      onSuccess: invalidate,
    }),
  }
}

/** Approved request lines still to be ordered (up to 100, most urgent first). */
export function useOrderableLines(search: string, enabled = true) {
  return useQuery({
    queryKey: purchasingKeys.orderable(search),
    queryFn: async () =>
      (await api.get<PagedResult<OrderableLine>>('/purchase-requests/orderable-lines', { params: { search: search || undefined, pageSize: 100 } }))
        .data,
    enabled,
  })
}

/** Latest approved price per material, preferring the supplier. */
export async function fetchLastPrices(materialIds: string[], supplierId: string | undefined): Promise<LastPrice[]> {
  if (materialIds.length === 0) return []
  const params = new URLSearchParams()
  for (const id of materialIds) params.append('materialIds', id)
  if (supplierId) params.set('supplierId', supplierId)
  return (await api.get<LastPrice[]>('/purchase-orders/last-prices', { params })).data
}

export function usePriceHistory(materialId: string | undefined, page: number, pageSize: number) {
  return useQuery({
    queryKey: [...purchasingKeys.reports, 'prices', materialId, page, pageSize],
    queryFn: async () =>
      (await api.get<PagedResult<PriceHistoryRow>>('/purchase-orders/price-history', { params: { materialId, page, pageSize } })).data,
    enabled: !!materialId,
    placeholderData: keepPreviousData,
  })
}

// --- Reports ------------------------------------------------------------------------------

export function usePurchasingSummary(params: { from?: string; to?: string }, enabled = true) {
  return useQuery({
    queryKey: [...purchasingKeys.reports, 'summary', params],
    queryFn: async () => (await api.get<PurchasingSummary>('/purchasing/reports/summary', { params })).data,
    enabled,
  })
}

export function useOverdueLines(page: number, pageSize: number, enabled = true) {
  return useQuery({
    queryKey: [...purchasingKeys.reports, 'overdue', page, pageSize],
    queryFn: async () => (await api.get<PagedResult<OverdueLine>>('/purchasing/reports/overdue-lines', { params: { page, pageSize } })).data,
    enabled,
    placeholderData: keepPreviousData,
  })
}
