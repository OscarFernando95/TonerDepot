import { http } from './http'
import type { AssetModelDto, CreateAssetModelRequest, UpdateAssetModelRequest } from './types'

export function listAssetModels(brandId: string) {
  return http.get<AssetModelDto[]>(`/asset-brands/${brandId}/models`)
}

export function createAssetModel(brandId: string, request: CreateAssetModelRequest) {
  return http.post<AssetModelDto>(`/asset-brands/${brandId}/models`, request)
}

export function updateAssetModel(brandId: string, id: string, request: UpdateAssetModelRequest) {
  return http.put<AssetModelDto>(`/asset-brands/${brandId}/models/${id}`, request)
}
