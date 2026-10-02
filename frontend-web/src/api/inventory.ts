import { http } from './http'
import { getPaged } from './paging'

export type InventoryCategory = 'ConsumibleBase' | 'Repuesto' | 'Toner'

export const InventoryCategoryLabels: Record<InventoryCategory, string> = {
  ConsumibleBase: 'Consumible base',
  Repuesto: 'Repuesto',
  Toner: 'Tóner'
}

export const InventoryCategories = Object.keys(InventoryCategoryLabels) as InventoryCategory[]

export interface InventoryItemDto {
  id: string
  name: string
  category: InventoryCategory
  unit: string | null
  unitCost: number | null
  minimumStock: number
  isActive: boolean
}

export interface InventoryItemRequest {
  name: string
  category: InventoryCategory
  unit?: string | null
  unitCost?: number | null
  minimumStock: number
  isActive?: boolean
}

export interface InventoryLocationDto {
  id: string
  kind: 'Principal' | 'Zona'
  name: string
  zoneId: string | null
  address: string | null
  cityId: string | null
  cityName: string | null
}

export interface StockRowDto {
  itemId: string
  itemName: string
  category: InventoryCategory
  locationId: string
  locationName: string
  quantity: number
  minimumStock: number
  isLow: boolean
}

export interface InventoryMovementDto {
  id: string
  itemId: string
  itemName: string
  locationId: string
  locationName: string
  type: 'Entrada' | 'TraspasoSalida' | 'TraspasoEntrada' | 'Consumo' | 'Ajuste'
  delta: number
  notes: string | null
  createdByUserName: string | null
  occurredAt: string
}

export const MovementTypeLabels: Record<InventoryMovementDto['type'], string> = {
  Entrada: 'Entrada',
  TraspasoSalida: 'Traspaso (sale)',
  TraspasoEntrada: 'Traspaso (entra)',
  Consumo: 'Consumo',
  Ajuste: 'Ajuste'
}

export interface BaseKitItemDto {
  itemId: string
  itemName: string
  category: InventoryCategory
  groupName: string
  quantity: number
}

export interface ModelKitItemDto extends BaseKitItemDto {
  source: 'Marca' | 'Modelo'
  excluded: boolean
}

export interface ModelKitOverride {
  itemId: string
  excluded: boolean
  groupName?: string | null
  quantity?: number | null
}

export function listItems(params: { search?: string; category?: string; activeOnly?: boolean; page?: number; pageSize?: number }) {
  return getPaged<InventoryItemDto>('/inventory/items', params as never)
}

export function createItem(request: InventoryItemRequest) {
  return http.post<InventoryItemDto>('/inventory/items', request)
}

export function updateItem(id: string, request: InventoryItemRequest) {
  return http.put<InventoryItemDto>(`/inventory/items/${id}`, request)
}

export function listLocations() {
  return http.get<InventoryLocationDto[]>('/inventory/locations')
}

export function updateMainLocation(request: { name: string; address?: string | null; cityId?: string | null }) {
  return http.put<InventoryLocationDto>('/inventory/locations/main', request)
}

export function listStock(params: { locationId?: string; itemId?: string; category?: string; onlyLow?: boolean; page?: number; pageSize?: number }) {
  return getPaged<StockRowDto>('/inventory/stock', params as never)
}

export function listMovements(params: { locationId?: string; itemId?: string; cursor?: string; pageSize?: number }) {
  return getPaged<InventoryMovementDto>('/inventory/movements', params as never)
}

export function registerEntry(request: { locationId: string; itemId: string; quantity: number; notes?: string | null }) {
  return http.post<InventoryMovementDto>('/inventory/entries', request)
}

export function transfer(request: { itemId: string; fromLocationId: string; toLocationId: string; quantity: number; notes?: string | null }) {
  return http.post<InventoryMovementDto[]>('/inventory/transfers', request)
}

export function adjust(request: { locationId: string; itemId: string; delta: number; notes: string }) {
  return http.post<InventoryMovementDto>('/inventory/adjustments', request)
}

export function getBrandKit(brandId: string) {
  return http.get<BaseKitItemDto[]>(`/asset-brands/${brandId}/base-items`)
}

export function setBrandKit(brandId: string, items: { itemId: string; groupName: string; quantity: number }[]) {
  return http.put<BaseKitItemDto[]>(`/asset-brands/${brandId}/base-items`, { items })
}

export function getModelKit(brandId: string, modelId: string) {
  return http.get<ModelKitItemDto[]>(`/asset-brands/${brandId}/models/${modelId}/base-items`)
}

export function setModelKit(brandId: string, modelId: string, overrides: ModelKitOverride[]) {
  return http.put<ModelKitItemDto[]>(`/asset-brands/${brandId}/models/${modelId}/base-items`, { overrides })
}

// ── Inventario en la visita y tóner por máquina ───────────────────────────────────────────────────

export interface VisitKitItemDto {
  itemId: string
  itemName: string
  category: InventoryCategory
  groupName: string
  quantity: number
  // Saldo en la ubicación de inventario de la zona del equipo.
  stock: number
}

export interface VisitKitDto {
  assetId: string | null
  locationId: string
  locationName: string
  // El municipio del equipo no tiene zona (o no está catalogado): se descuenta de la bodega principal.
  usesMainWarehouse: boolean
  items: VisitKitItemDto[]
}

export interface PartOptionDto {
  itemId: string
  name: string
  category: InventoryCategory
  unit: string | null
  stock: number
}

export interface TonerEntryDto {
  movementId: string
  itemId: string
  itemName: string
  quantity: number
  occurredAt: string
  counterValue: number | null
  notes: string | null
  registeredBy: string | null
  stockWarning: string | null
}

export function getVisitKit(assetId?: string | null) {
  return http.get<VisitKitDto>('/inventory/kit', { params: { assetId: assetId || undefined } })
}

export function searchParts(params: { assetId?: string | null; search?: string; category?: InventoryCategory; pageSize?: number }) {
  return getPaged<PartOptionDto>('/inventory/parts', { ...params, assetId: params.assetId || undefined } as never)
}

export function registerToner(request: {
  assetId: string
  itemId: string
  quantity: number
  occurredAt?: string | null
  deliveredToUser: boolean
  counterValue?: number | null
  notes?: string | null
}) {
  return http.post<TonerEntryDto>('/inventory/toner', request)
}

export function listToner(assetId: string, params?: { from?: string; to?: string; cursor?: string; pageSize?: number }) {
  return getPaged<TonerEntryDto>(`/inventory/toner/assets/${assetId}`, params as never)
}
