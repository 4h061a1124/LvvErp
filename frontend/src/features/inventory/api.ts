import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { PagedResult } from '@/lib/types'
import type {
  CatalogItem,
  CatalogItemInput,
  CatalogResource,
  Location,
  LocationInput,
  Material,
  MaterialInput,
  MaterialListParams,
  Warehouse,
  WarehouseInput,
} from './types'

export const inventoryKeys = {
  catalog: (resource: CatalogResource) => ['inventory', resource] as const,
  warehouses: ['inventory', 'warehouses'] as const,
  locations: (warehouseId: string) => ['inventory', 'warehouses', warehouseId, 'locations'] as const,
  materials: ['inventory', 'materials'] as const,
  materialList: (params: MaterialListParams) => ['inventory', 'materials', 'list', params] as const,
}

// --- Units / material groups -------------------------------------------------------------

/** Full list (catalog pages) or only active entries (pickers). */
export function useCatalog(resource: CatalogResource, activeOnly = false) {
  return useQuery({
    queryKey: [...inventoryKeys.catalog(resource), { activeOnly }],
    queryFn: async () => (await api.get<CatalogItem[]>(`/${resource}`, { params: { activeOnly } })).data,
    staleTime: 5 * 60_000,
  })
}

export function useSaveCatalogItem(resource: CatalogResource) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: CatalogItemInput }) =>
      id
        ? (await api.put<CatalogItem>(`/${resource}/${id}`, input)).data
        : (await api.post<CatalogItem>(`/${resource}`, input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.catalog(resource) }),
  })
}

export function useDeleteCatalogItem(resource: CatalogResource) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/${resource}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.catalog(resource) }),
  })
}

// --- Warehouses and locations -------------------------------------------------------------

export function useWarehouses(activeOnly = false) {
  return useQuery({
    queryKey: [...inventoryKeys.warehouses, { activeOnly }],
    queryFn: async () => (await api.get<Warehouse[]>('/warehouses', { params: { activeOnly } })).data,
    staleTime: 5 * 60_000,
  })
}

export function useSaveWarehouse() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: WarehouseInput }) =>
      id
        ? (await api.put<Warehouse>(`/warehouses/${id}`, input)).data
        : (await api.post<Warehouse>('/warehouses', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.warehouses }),
  })
}

export function useDeleteWarehouse() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/warehouses/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.warehouses }),
  })
}

export function useLocations(warehouseId: string | undefined, activeOnly = false) {
  return useQuery({
    queryKey: [...inventoryKeys.locations(warehouseId ?? ''), { activeOnly }],
    queryFn: async () =>
      (await api.get<Location[]>(`/warehouses/${warehouseId}/locations`, { params: { activeOnly } })).data,
    enabled: !!warehouseId,
    staleTime: 5 * 60_000,
  })
}

export function useSaveLocation(warehouseId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: LocationInput }) =>
      id
        ? (await api.put<Location>(`/locations/${id}`, input)).data
        : (await api.post<Location>(`/warehouses/${warehouseId}/locations`, input)).data,
    // The warehouse list shows a location count.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.warehouses }),
  })
}

export function useDeleteLocation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/locations/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.warehouses }),
  })
}

// --- Materials ----------------------------------------------------------------------------

export function useMaterials(params: MaterialListParams) {
  return useQuery({
    queryKey: inventoryKeys.materialList(params),
    queryFn: async () => (await api.get<PagedResult<Material>>('/materials', { params })).data,
    placeholderData: keepPreviousData,
  })
}

export function useSaveMaterial() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: string; input: MaterialInput }) =>
      id
        ? (await api.put<Material>(`/materials/${id}`, input)).data
        : (await api.post<Material>('/materials', input)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.materials }),
  })
}

export function useDeleteMaterial() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(`/materials/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: inventoryKeys.materials }),
  })
}

export async function uploadMaterialPhoto(id: string, file: File) {
  const form = new FormData()
  form.append('file', file)
  await api.post(`/materials/${id}/photo`, form)
}

export async function deleteMaterialPhoto(id: string) {
  await api.delete(`/materials/${id}/photo`)
}
