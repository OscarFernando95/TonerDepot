import { getList, getPaged } from './paging'
import { http } from './http'
import type { MaintenanceOrderDto, MaintenanceScheduleDto } from './types'

export function listMaintenanceSchedules() {
  return getList<MaintenanceScheduleDto>('/maintenance-schedules')
}

// Listado paginado y filtrado en el servidor (AUDIT.md H-11). Los filtros vacíos no se envían.
export interface MaintenanceScheduleListParams {
  search?: string
  cityId?: string
  clientId?: string
  contractId?: string
  isActive?: boolean
  page?: number
  pageSize?: number
}

export function listMaintenanceSchedulesPaged(params: MaintenanceScheduleListParams) {
  return getPaged<MaintenanceScheduleDto>('/maintenance-schedules', params as Record<string, unknown>)
}

export function getMaintenanceSchedule(id: string) {
  return http.get<MaintenanceScheduleDto>(`/maintenance-schedules/${id}`)
}

export function setMaintenanceScheduleStatus(id: string, isActive: boolean) {
  return http.patch<MaintenanceScheduleDto>(`/maintenance-schedules/${id}/status`, { isActive })
}

export function getScheduleOrders(id: string) {
  return getList<MaintenanceOrderDto>(`/maintenance-schedules/${id}/orders`)
}

// Crea el cronograma de activos ya Instalados con contrato activo que quedaron sin uno.
export function backfillMaintenanceSchedules() {
  return http.post<{ created: number }>('/maintenance-schedules/backfill')
}

export function evaluateSchedulesNow() {
  return http.post<{ ordersCreated: number }>('/maintenance-schedules/evaluate-now')
}
