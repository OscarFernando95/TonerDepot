import { getList, getPaged } from './paging'
import { http } from './http'
import type {
  AssetDto,
  AssetStatusLogDto,
  ChangeAssetStatusRequest,
  CreateAssetRequest,
  CreateMeterReadingRequest,
  MeterReadingDto,
  UpdateAssetRequest
} from './types'

// Filtros del listado de activos, aplicados en el SERVIDOR antes de paginar (AUDIT.md H-11, Paso 3). Los vacíos no se
// envían. `status` es un AssetLifecycleStatusName.
export interface AssetListParams {
  search?: string
  cityId?: string
  clientId?: string
  clientLocationId?: string
  status?: string
  page?: number
  pageSize?: number
}

export function listAssetsPaged(params: AssetListParams) {
  return getPaged<AssetDto>('/assets', params as Record<string, unknown>)
}

// Todas las páginas de un listado ACOTADO por filtros (activos de una sede, instalados, en bodega…), para selectores y
// acciones masivas. Antes los selectores pedían TODO el inventario y se truncaban en silencio a 200 (H-11); aquí se
// recorren las páginas hasta agotarlas. Úsalo solo con un filtro que acote el resultado: para una tabla, listAssetsPaged.
const ALL_PAGE_SIZE = 200
const ALL_MAX_PAGES = 50

export async function listAllAssets(filter: Omit<AssetListParams, 'page' | 'pageSize'> = {}): Promise<AssetDto[]> {
  const all: AssetDto[] = []
  for (let page = 1; page <= ALL_MAX_PAGES; page++) {
    const { data } = await listAssetsPaged({ ...filter, page, pageSize: ALL_PAGE_SIZE })
    all.push(...data.items)
    if (!data.hasMore) return all
  }
  console.error(`listAllAssets: se cortó en ${ALL_MAX_PAGES * ALL_PAGE_SIZE} activos; acota el filtro`, filter)
  return all
}

export function getAsset(id: string) {
  return http.get<AssetDto>(`/assets/${id}`)
}

export function createAsset(request: CreateAssetRequest) {
  return http.post<AssetDto>('/assets', request)
}

export function updateAsset(id: string, request: UpdateAssetRequest) {
  return http.put<AssetDto>(`/assets/${id}`, request)
}

export function changeAssetStatus(id: string, request: ChangeAssetStatusRequest) {
  return http.post<AssetDto>(`/assets/${id}/status`, request)
}

export function getAssetStatusHistory(id: string) {
  return getList<AssetStatusLogDto>(`/assets/${id}/status-history`)
}

export function addMeterReading(id: string, request: CreateMeterReadingRequest) {
  return http.post<MeterReadingDto>(`/assets/${id}/meter-readings`, request)
}

export function getMeterReadings(id: string) {
  return getList<MeterReadingDto>(`/assets/${id}/meter-readings`)
}
