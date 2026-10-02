import { getList } from './paging'
import { http } from './http'

export interface TechnicianDto {
  id: string
  fullName: string
  phone: string | null
  status: string
  isActive: boolean
  // Dentro de su horario laboral ahora mismo (día hábil, tramo vigente, sin permiso).
  isWorkingNow: boolean
  // Si está fuera de la oficina ahora, hasta cuándo (ISO UTC).
  timeOffUntil: string | null
  // Zonas asignadas y los municipios que cubre a través de ellas.
  zoneNames: string[]
  coverageCityNames: string[]
}

export interface WorkIntervalDto {
  // 0 = domingo ... 6 = sábado
  day: number
  // HH:mm, hora local de la empresa
  start: string
  end: string
}

export interface TechnicianScheduleDto {
  // true: no tiene horario propio, se muestra el de la empresa
  isDefault: boolean
  timeZoneId: string
  intervals: WorkIntervalDto[]
}

export interface TimeOffDto {
  id: string
  startsAt: string
  endsAt: string
  effectiveEndsAt: string
  reason: string | null
  cancelledAt: string | null
  isActive: boolean
}

export interface TechnicianZoneDto {
  zoneId: string
  zoneName: string
  cityNames: string[]
}

export interface TechnicianAssetDto {
  id: string
  assetId: string
  assetBrandName: string
  model: string
  serialNumber: string
  clientName: string | null
  clientLocationName: string | null
  cityName: string | null
  area: string | null
}

export function listTechnicians() {
  return getList<TechnicianDto>('/technicians')
}

export function listTechnicianZones(technicianId: string) {
  return http.get<TechnicianZoneDto[]>(`/technicians/${technicianId}/zones`)
}

// Reemplaza el conjunto de zonas del técnico (normalmente una).
export function setTechnicianZones(technicianId: string, zoneIds: string[]) {
  return http.put<TechnicianZoneDto[]>(`/technicians/${technicianId}/zones`, { zoneIds })
}

export function getTechnicianSchedule(technicianId: string) {
  return http.get<TechnicianScheduleDto>(`/technicians/${technicianId}/schedule`)
}

export function setTechnicianSchedule(technicianId: string, intervals: WorkIntervalDto[]) {
  return http.put<TechnicianScheduleDto>(`/technicians/${technicianId}/schedule`, { intervals })
}

// Borra el horario propio: vuelve a aplicar el de la empresa.
export function resetTechnicianSchedule(technicianId: string) {
  return http.delete<TechnicianScheduleDto>(`/technicians/${technicianId}/schedule`)
}

export function listTechnicianTimeOff(technicianId: string) {
  return http.get<TimeOffDto[]>(`/technicians/${technicianId}/time-off`)
}

export function addTechnicianTimeOff(technicianId: string, startsAt: string, endsAt: string, reason: string | null) {
  return http.post<TimeOffDto>(`/technicians/${technicianId}/time-off`, { startsAt, endsAt, reason })
}

export function cancelTechnicianTimeOff(technicianId: string, timeOffId: string) {
  return http.post<TimeOffDto>(`/technicians/${technicianId}/time-off/${timeOffId}/cancel`)
}

export interface TimeLogDto {
  id: string
  serviceTicketId: string | null
  maintenanceOrderId: string | null
  startTime: string
  endTime: string | null
  notes: string | null
  // EnSitio | FueraDeSitio | SinUbicacion | SedeSinCoordenadas
  checkInLocationStatus: string | null
  checkInDistanceMeters: number | null
  checkOutLocationStatus: string | null
  checkOutDistanceMeters: number | null
}

export function listTechnicianVisits(technicianId: string) {
  return getList<TimeLogDto>(`/technicians/${technicianId}/time-logs`)
}

export function listTechnicianAssets(technicianId: string) {
  return http.get<TechnicianAssetDto[]>(`/technicians/${technicianId}/assets`)
}

export function addTechnicianAsset(technicianId: string, assetId: string) {
  return http.post<TechnicianAssetDto>(`/technicians/${technicianId}/assets`, { assetId })
}

export function removeTechnicianAsset(technicianId: string, technicianAssetId: string) {
  return http.delete(`/technicians/${technicianId}/assets/${technicianAssetId}`)
}
