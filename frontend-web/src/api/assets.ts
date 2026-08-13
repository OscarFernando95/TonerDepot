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

export function listAssets() {
  return http.get<AssetDto[]>('/assets')
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
  return http.get<AssetStatusLogDto[]>(`/assets/${id}/status-history`)
}

export function addMeterReading(id: string, request: CreateMeterReadingRequest) {
  return http.post<MeterReadingDto>(`/assets/${id}/meter-readings`, request)
}

export function getMeterReadings(id: string) {
  return http.get<MeterReadingDto[]>(`/assets/${id}/meter-readings`)
}
