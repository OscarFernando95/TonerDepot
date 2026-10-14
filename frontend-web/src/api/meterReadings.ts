import { getPaged } from './paging'
import { http } from './http'
import type { CreateMeterReadingRequest, MeterReadingAssetDto, MeterReadingDto } from './types'

// Listado paginado y filtrado en el servidor (AUDIT.md H-11, Paso 3). Los filtros vacíos no se envían.
export interface MeterReadingListParams {
  search?: string
  cityId?: string
  clientId?: string
  page?: number
  pageSize?: number
}

// Módulo abierto a Staff y Técnico.
export function listMeterReadingAssets(params: MeterReadingListParams = {}) {
  return getPaged<MeterReadingAssetDto>('/meter-readings', params as Record<string, unknown>)
}

// Pestaña "Por cobertura" — solo Técnico (espejo de MeterReadingsScreen en el móvil): activos
// instalados en los municipios de su zona asignada, estén o no vinculados explícitamente. Ver
// AssetService.ListForMeterReadingByCoverageAsync, backend.
export function listMeterReadingAssetsByCoverage(params: MeterReadingListParams = {}) {
  return getPaged<MeterReadingAssetDto>('/meter-readings/by-coverage', params as Record<string, unknown>)
}

export function registerMeterReading(assetId: string, request: CreateMeterReadingRequest) {
  return http.post<MeterReadingDto>(`/meter-readings/${assetId}`, request)
}
