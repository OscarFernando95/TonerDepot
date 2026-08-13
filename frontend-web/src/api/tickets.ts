import { http } from './http'
import type { AssignTicketRequest, AssignmentHistoryDto, CreateServiceTicketRequest, ServiceTicketDto } from './types'

export function listTickets() {
  return http.get<ServiceTicketDto[]>('/tickets')
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
  return http.get<AssignmentHistoryDto[]>(`/tickets/${id}/assignment-history`)
}
