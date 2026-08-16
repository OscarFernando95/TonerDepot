import { http } from './http'
import type { CreateMeterReadingRequest, MeterReadingAssetDto, MeterReadingDto } from './types'

// Módulo abierto a los 5 roles.
export function listMeterReadingAssets() {
  return http.get<MeterReadingAssetDto[]>('/meter-readings')
}

export function registerMeterReading(assetId: string, request: CreateMeterReadingRequest) {
  return http.post<MeterReadingDto>(`/meter-readings/${assetId}`, request)
}
