import { getList } from './paging'
import { http } from './http'
import type { CreateMeterReadingRequest, MeterReadingAssetDto, MeterReadingDto } from './types'

// Módulo abierto a los 5 roles.
export function listMeterReadingAssets() {
  return getList<MeterReadingAssetDto>('/meter-readings')
}

// Pestaña "Por cobertura" — solo Técnico (espejo de MeterReadingsScreen en el móvil): activos
// instalados en los municipios de su zona asignada, estén o no vinculados explícitamente. Ver
// AssetService.ListForMeterReadingByCoverageAsync, backend.
export function listMeterReadingAssetsByCoverage() {
  return getList<MeterReadingAssetDto>('/meter-readings/by-coverage')
}

export function registerMeterReading(assetId: string, request: CreateMeterReadingRequest) {
  return http.post<MeterReadingDto>(`/meter-readings/${assetId}`, request)
}
