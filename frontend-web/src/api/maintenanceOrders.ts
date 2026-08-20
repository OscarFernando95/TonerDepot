import { getList } from './paging'
import { http } from './http'
import type { AssignmentHistoryDto, CompleteMaintenanceOrderRequest, MaintenanceOrderDto } from './types'

export function listMaintenanceOrders() {
  return getList<MaintenanceOrderDto>('/maintenance-orders')
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
