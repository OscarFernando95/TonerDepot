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
  // Ubicación del técnico (opcional: si falta, el servidor la registra como "sin ubicación" y alerta).
  latitude?: number | null
  longitude?: number | null
  accuracyMeters?: number | null
  // Foto "antes" ya subida con uploadEvidence — obligatoria para tickets y órdenes.
  beforeEvidenceId?: string | null
}

export interface CheckOutRequest {
  latitude?: number | null
  longitude?: number | null
  accuracyMeters?: number | null
  // Foto "después" — obligatoria al resolver un ticket u orden.
  afterEvidenceId?: string | null
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
  // Vigencia del contrato activo del activo (null si no tiene contrato vinculado) — solo para
  // restringir el selector de fecha del check-out en la UI; la validación real vive en el backend.
  contractStartDate: string | null
  contractEndDate: string | null
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

export interface EvidenceDto {
  id: string
  kind: 'Antes' | 'Despues'
  contentType: string
  sizeBytes: number
  uploadedAt: string
  serviceTicketId: string | null
  maintenanceOrderId: string | null
  timeLogId: string | null
}

// Sube la foto de evidencia (antes / después) de un ticket u orden asignado; devuelve el id que se envía en el check-in/out.
export function uploadEvidence(file: File, kind: 'Antes' | 'Despues', target: { ticketId?: string; orderId?: string }) {
  const form = new FormData()
  form.append('file', file)
  form.append('kind', kind)
  if (target.ticketId) form.append('ticketId', target.ticketId)
  if (target.orderId) form.append('orderId', target.orderId)
  return http.post<EvidenceDto>('/technicians/me/evidence', form, { timeout: 60000 })
}
