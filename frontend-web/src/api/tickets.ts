import { getList, getPaged } from './paging'
import { http } from './http'
import type { AssignTicketRequest, AssignmentHistoryDto, CreateServiceTicketRequest, ServiceTicketDto } from './types'

export function listTickets() {
  return getList<ServiceTicketDto>('/tickets')
}

// Listado paginado y filtrado en el servidor (AUDIT.md H-11). Los filtros vacíos no se envían.
export interface TicketListParams {
  status?: string
  priority?: string
  clientId?: string
  search?: string
  page?: number
  pageSize?: number
}

export function listTicketsPaged(params: TicketListParams) {
  return getPaged<ServiceTicketDto>('/tickets', params as Record<string, unknown>)
}

export function getTicket(id: string) {
  return http.get<ServiceTicketDto>(`/tickets/${id}`)
}

export function createTicket(request: CreateServiceTicketRequest) {
  return http.post<ServiceTicketDto>('/tickets', request)
}

export function assignTicket(id: string, request: AssignTicketRequest) {
  return http.post<ServiceTicketDto>(`/tickets/${id}/assign`, request)
}

export function setTicketStatus(id: string, status: string) {
  return http.patch<ServiceTicketDto>(`/tickets/${id}/status`, { status })
}

export function getTicketAssignmentHistory(id: string) {
  return getList<AssignmentHistoryDto>(`/tickets/${id}/assignment-history`)
}
