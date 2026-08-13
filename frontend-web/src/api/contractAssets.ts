import { http } from './http'
import type { AddContractAssetRequest, ContractAssetDto } from './types'

export function listContractAssets(contractId: string) {
  return http.get<ContractAssetDto[]>(`/contracts/${contractId}/assets`)
}

export function addContractAsset(contractId: string, request: AddContractAssetRequest) {
  return http.post<ContractAssetDto>(`/contracts/${contractId}/assets`, request)
}

export function endContractAsset(contractId: string, id: string) {
  return http.patch<ContractAssetDto>(`/contracts/${contractId}/assets/${id}/end`)
}
