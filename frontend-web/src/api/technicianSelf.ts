import { getList } from './paging'
import { http } from './http'
import type { MaintenanceOrderDto, MaintenanceScheduleDto, ServiceTicketDto } from './types'

export interface TechnicianSelfStatusDto {
  technicianId: string
  status: string
  activeServiceTicketId: string | null
  activeMaintenanceOrderId: string | null
  activeAssetInstallationId: string | null
  checkedInAt: string | null
}

export interface CheckInRequest {
  serviceTicketId?: string | null
  maintenanceOrderId?: string | null
  assetId?: string | null
}

export interface CheckOutRequest {
  resolved?: boolean
  notes?: string | null
  area?: string | null
  // Obligatorio al cerrar una instalación o una orden de mantenimiento; opcional al cerrar un ticket.
  initialCounterValue?: number | null
  initialCounterDate?: string | null
  // Obligatorios solo al cerrar una instalación. unitsMaintenanceDone e "insumos nuevos" son mutuamente
  // excluyentes: false implica insumos nuevos (offset 0); true revela existingConsumablesPrints.
  generalMaintenanceDone?: boolean | null
  unitsMaintenanceDone?: boolean | null
  existingConsumablesPrints?: number | null

  // Totalmente opcionales: solo tienen efecto al cerrar un ticket sin Asset asociado (cliente externo).
  externalAssetBrand?: string | null
  externalAssetModel?: string | null
  externalAssetCounter?: number | null
}

export interface PendingInstallationDto {
  assetId: string
  assetBrandName: string
  model: string
  serialNumber: string
  clientId: string
  clientName: string
  clientLocationId: string
  clientLocationName: string
  cityName: string | null
  contractId: string | null
  takenByAnotherTechnician: boolean
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

export function listPendingInstallations() {
  return getList<PendingInstallationDto>('/technicians/me/pending-installations')
}

export function listCoverageMaintenanceOrders() {
  return getList<MaintenanceOrderDto>('/technicians/me/coverage-maintenance-orders')
}

export function listCoverageSchedules() {
  return getList<MaintenanceScheduleDto>('/technicians/me/coverage-schedules')
}

export function listCoverageTickets() {
  return getList<ServiceTicketDto>('/technicians/me/coverage-tickets')
}

export function claimMaintenanceOrder(id: string) {
  return http.post<MaintenanceOrderDto>(`/technicians/me/maintenance-orders/${id}/claim`)
}

export function claimTicket(id: string) {
  return http.post<ServiceTicketDto>(`/technicians/me/tickets/${id}/claim`)
}
