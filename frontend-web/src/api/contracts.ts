import { getList } from './paging'
import { http } from './http'
import type { ContractDto, CreateContractRequest, UpdateContractRequest } from './types'

export function listContracts() {
  return getList<ContractDto>('/contracts')
}

export function getContract(id: string) {
  return http.get<ContractDto>(`/contracts/${id}`)
}

export function createContract(request: CreateContractRequest) {
  return http.post<ContractDto>('/contracts', request)
}

export function updateContract(id: string, request: UpdateContractRequest) {
  return http.put<ContractDto>(`/contracts/${id}`, request)
}

export function setContractStatus(id: string, status: string) {
  return http.patch<ContractDto>(`/contracts/${id}/status`, { status })
}
