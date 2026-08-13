import { http } from './http'
import type { ClientDto, CreateClientRequest, UpdateClientRequest } from './types'

export function listClients() {
  return http.get<ClientDto[]>('/clients')
}

export function getClient(id: string) {
  return http.get<ClientDto>(`/clients/${id}`)
}

export function createClient(request: CreateClientRequest) {
  return http.post<ClientDto>('/clients', request)
}

export function updateClient(id: string, request: UpdateClientRequest) {
  return http.put<ClientDto>(`/clients/${id}`, request)
}

export function setClientStatus(id: string, isActive: boolean) {
  return http.patch<ClientDto>(`/clients/${id}/status`, { isActive })
}
