import { http } from './http'
import type { ClientLocationDto, CreateClientLocationRequest, UpdateClientLocationRequest } from './types'

export function listClientLocations(clientId: string) {
  return http.get<ClientLocationDto[]>(`/clients/${clientId}/locations`)
}

export function listAllLocations() {
  return http.get<ClientLocationDto[]>('/locations')
}

export function createClientLocation(clientId: string, request: CreateClientLocationRequest) {
  return http.post<ClientLocationDto>(`/clients/${clientId}/locations`, request)
}

export function updateClientLocation(clientId: string, id: string, request: UpdateClientLocationRequest) {
  return http.put<ClientLocationDto>(`/clients/${clientId}/locations/${id}`, request)
}

export function setClientLocationStatus(clientId: string, id: string, isActive: boolean) {
  return http.patch<ClientLocationDto>(`/clients/${clientId}/locations/${id}/status`, { isActive })
}
