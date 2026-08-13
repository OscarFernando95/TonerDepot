import { http } from './http'
import type { AssetBrandDto, CreateAssetBrandRequest } from './types'

export function listAssetBrands() {
  return http.get<AssetBrandDto[]>('/asset-brands')
}

export function createAssetBrand(request: CreateAssetBrandRequest) {
  return http.post<AssetBrandDto>('/asset-brands', request)
}
