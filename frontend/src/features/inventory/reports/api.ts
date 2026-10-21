import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { PagedResult } from '@/lib/types'
import { stockKeys } from '../documents/api'
import type { DocumentReason, DocumentType } from '../documents/types'

export interface StockOnHand {
  materialId: string
  code: string
  name: string
  specification: string | null
  unitName: string
  groupId: string
  groupName: string
  minStock: number
  quantity: number
  totalQuantity: number
  isBelowMin: boolean
  trackLots: boolean
  photoUrl: string | null
}

export interface StockOnHandParams {
  warehouseId?: string
  groupId?: string
  search?: string
  belowMinOnly?: boolean
  includeZero?: boolean
  page: number
  pageSize: number
  sort?: string
}

export interface StockOnHandDetail {
  warehouseId: string
  warehouseName: string
  locationId: string | null
  locationCode: string | null
  lotId: string | null
  lotNumber: string | null
  expiryDate: string | null
  quantity: number
}

export interface BelowMinStock {
  materialId: string
  code: string
  name: string
  unitName: string
  minStock: number
  quantity: number
  shortage: number
}

export interface StockCardEntry {
  transactionId: string
  date: string
  documentId: string
  documentNumber: string
  documentType: DocumentType
  reason: DocumentReason | null
  isReversal: boolean
  warehouseName: string
  locationCode: string | null
  lotNumber: string | null
  partner: string | null
  recipient: string | null
  otherWarehouseName: string | null
  in: number
  out: number
  balance: number
}

export interface StockCard {
  materialId: string
  materialCode: string
  materialName: string
  unitName: string
  warehouseId: string | null
  warehouseName: string | null
  opening: number
  totalIn: number
  totalOut: number
  closing: number
  truncated: boolean
  entries: StockCardEntry[]
}

export function useStockOnHand(params: StockOnHandParams) {
  return useQuery({
    queryKey: [...stockKeys.all, 'on-hand', params],
    queryFn: async () => (await api.get<PagedResult<StockOnHand>>('/stock/on-hand', { params })).data,
    placeholderData: keepPreviousData,
  })
}

export function useStockOnHandDetail(materialId: string, warehouseId: string | undefined) {
  return useQuery({
    queryKey: [...stockKeys.all, 'on-hand-detail', materialId, warehouseId],
    queryFn: async () =>
      (await api.get<StockOnHandDetail[]>(`/stock/on-hand/${materialId}`, { params: { warehouseId } })).data,
  })
}

export function useBelowMinStock() {
  return useQuery({
    queryKey: [...stockKeys.all, 'below-min'],
    queryFn: async () => (await api.get<BelowMinStock[]>('/stock/below-min')).data,
  })
}

export function useStockCard(params: { materialId?: string; warehouseId?: string; from?: string; to?: string }) {
  return useQuery({
    queryKey: [...stockKeys.all, 'card', params],
    queryFn: async () => (await api.get<StockCard>('/stock/card', { params })).data,
    enabled: !!params.materialId,
    placeholderData: keepPreviousData,
  })
}
