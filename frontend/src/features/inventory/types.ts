export interface CatalogItem {
  id: string
  code: string
  name: string
  isActive: boolean
}

export interface CatalogItemInput {
  code: string
  name: string
  isActive: boolean
}

/** Simple code/name catalogs served by the same endpoints shape. */
export type CatalogResource = 'units' | 'material-groups'

export interface Warehouse {
  id: string
  code: string
  name: string
  note: string | null
  isActive: boolean
  locationCount: number
}

export interface WarehouseInput {
  code: string
  name: string
  note: string | null
  isActive: boolean
}

export interface Location {
  id: string
  warehouseId: string
  code: string
  description: string | null
  isActive: boolean
}

export interface LocationInput {
  code: string
  description: string | null
  isActive: boolean
}

export interface Material {
  id: string
  code: string
  name: string
  specification: string | null
  unitId: string
  unitName: string
  groupId: string
  groupName: string
  minStock: number
  trackLots: boolean
  receiptTolerancePercent: number
  photoUrl: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}

export interface MaterialInput {
  code: string
  name: string
  specification: string | null
  unitId: string
  groupId: string
  minStock: number
  trackLots: boolean
  receiptTolerancePercent: number
  isActive: boolean
}

export interface MaterialListParams {
  search?: string
  groupId?: string
  isActive?: boolean
  page: number
  pageSize: number
  sort?: string
}
