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

export interface TechnicianCoverageDto {
  id: string
  cityId: string
  cityName: string
}

export function listTechnicians() {
  return getList<TechnicianDto>('/technicians')
}

export function listTechnicianCoverage(technicianId: string) {
  return http.get<TechnicianCoverageDto[]>(`/technicians/${technicianId}/coverage`)
}

export function addTechnicianCoverage(technicianId: string, cityId: string) {
  return http.post<TechnicianCoverageDto>(`/technicians/${technicianId}/coverage`, { cityId })
}

export function removeTechnicianCoverage(technicianId: string, coverageId: string) {
  return http.delete(`/technicians/${technicianId}/coverage/${coverageId}`)
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
