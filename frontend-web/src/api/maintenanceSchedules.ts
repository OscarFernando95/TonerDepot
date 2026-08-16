import { http } from './http'
import type { MaintenanceOrderDto, MaintenanceScheduleDto } from './types'

export function listMaintenanceSchedules() {
  return http.get<MaintenanceScheduleDto[]>('/maintenance-schedules')
}

export function getMaintenanceSchedule(id: string) {
  return http.get<MaintenanceScheduleDto>(`/maintenance-schedules/${id}`)
}

export function setMaintenanceScheduleStatus(id: string, isActive: boolean) {
  return http.patch<MaintenanceScheduleDto>(`/maintenance-schedules/${id}/status`, { isActive })
}

export function getScheduleOrders(id: string) {
  return http.get<MaintenanceOrderDto[]>(`/maintenance-schedules/${id}/orders`)
}

// Crea el cronograma de activos ya Instalados con contrato activo que quedaron sin uno.
export function backfillMaintenanceSchedules() {
  return http.post<{ created: number }>('/maintenance-schedules/backfill')
}

export function evaluateSchedulesNow() {
  return http.post<{ ordersCreated: number }>('/maintenance-schedules/evaluate-now')
}
