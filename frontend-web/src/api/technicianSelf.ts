import { http } from './http'

export interface TechnicianSelfStatusDto {
  technicianId: string
  status: string
  activeServiceTicketId: string | null
  activeMaintenanceOrderId: string | null
  checkedInAt: string | null
}

export interface CheckInRequest {
  serviceTicketId?: string | null
  maintenanceOrderId?: string | null
}

export interface CheckOutRequest {
  resolved?: boolean
  notes?: string | null
}

export function getMyStatus() {
  return http.get<TechnicianSelfStatusDto>('/technicians/me/status')
}

export function checkIn(request: CheckInRequest) {
  return http.post<TechnicianSelfStatusDto>('/technicians/me/check-in', request)
}

export function checkOut(request: CheckOutRequest) {
  return http.post<TechnicianSelfStatusDto>('/technicians/me/check-out', request)
}
