import { http } from './http'
import type {
  CreateMaintenanceScheduleRequest,
  MaintenanceOrderDto,
  MaintenanceScheduleDto,
  UpdateMaintenanceScheduleRequest
} from './types'

export function listMaintenanceSchedules() {
  return http.get<MaintenanceScheduleDto[]>('/maintenance-schedules')
}

export function getMaintenanceSchedule(id: string) {
  return http.get<MaintenanceScheduleDto>(`/maintenance-schedules/${id}`)
}

export function createMaintenanceSchedule(request: CreateMaintenanceScheduleRequest) {
  return http.post<MaintenanceScheduleDto>('/maintenance-schedules', request)
}

export function updateMaintenanceSchedule(id: string, request: UpdateMaintenanceScheduleRequest) {
  return http.put<MaintenanceScheduleDto>(`/maintenance-schedules/${id}`, request)
}

export function setMaintenanceScheduleStatus(id: string, isActive: boolean) {
  return http.patch<MaintenanceScheduleDto>(`/maintenance-schedules/${id}/status`, { isActive })
}

export function getScheduleOrders(id: string) {
  return http.get<MaintenanceOrderDto[]>(`/maintenance-schedules/${id}/orders`)
}

export function evaluateSchedulesNow() {
  return http.post<{ ordersCreated: number }>('/maintenance-schedules/evaluate-now')
}
