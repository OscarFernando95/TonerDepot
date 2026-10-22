import { getList, getPaged } from './paging'
import { http } from './http'
import type {
  AssignmentHistoryDto,
  CompleteMaintenanceOrderRequest,
  CreateManualMaintenanceOrderRequest,
  MaintenanceOrderCommentDto,
  MaintenanceOrderDto
} from './types'

export function listMaintenanceOrders() {
  return getList<MaintenanceOrderDto>('/maintenance-orders')
}

// Listado paginado y filtrado en el servidor (AUDIT.md H-11). Los filtros vacíos no se envían.
export interface MaintenanceOrderListParams {
  status?: string
  clientId?: string
  search?: string
  page?: number
  pageSize?: number
}

export function listMaintenanceOrdersPaged(params: MaintenanceOrderListParams) {
  return getPaged<MaintenanceOrderDto>('/maintenance-orders', params as Record<string, unknown>)
}

// Mantenimiento a demanda de un activo instalado, fuera de los umbrales del cronograma.
export function createManualMaintenanceOrder(request: CreateManualMaintenanceOrderRequest) {
  return http.post<MaintenanceOrderDto>('/maintenance-orders', request)
}

export function getMaintenanceOrder(id: string) {
  return http.get<MaintenanceOrderDto>(`/maintenance-orders/${id}`)
}

export function assignMaintenanceOrder(id: string, technicianId: string, reason?: string | null) {
  return http.post<MaintenanceOrderDto>(`/maintenance-orders/${id}/assign`, { technicianId, reason })
}

export function completeMaintenanceOrder(id: string, request: CompleteMaintenanceOrderRequest) {
  return http.post<MaintenanceOrderDto>(`/maintenance-orders/${id}/complete`, request)
}

export function cancelMaintenanceOrder(id: string) {
  return http.post<MaintenanceOrderDto>(`/maintenance-orders/${id}/cancel`)
}

export function getMaintenanceOrderAssignmentHistory(id: string) {
  return getList<AssignmentHistoryDto>(`/maintenance-orders/${id}/assignment-history`)
}

// Hilo de comentarios y programaciones de la orden, en orden cronológico. Lo leen staff, el técnico asignado y el
// cliente dueño del equipo. El servidor devuelve un arreglo plano (el hilo de una orden es corto y acotado), no
// un PagedResult, por eso no pasa por getList.
export function listOrderComments(id: string) {
  return http.get<MaintenanceOrderCommentDto[]>(`/maintenance-orders/${id}/comments`)
}

// 409 si la orden ya está Completada o Cancelada (hilo cerrado, solo lectura).
export function addOrderComment(id: string, body: string) {
  return http.post<MaintenanceOrderCommentDto>(`/maintenance-orders/${id}/comments`, { body })
}
