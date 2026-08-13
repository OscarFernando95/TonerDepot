import { http } from './http'

export interface TechnicianDto {
  id: string
  fullName: string
  phone: string | null
  status: string
  isActive: boolean
  coverageCityNames: string[]
}

export interface TechnicianCoverageDto {
  id: string
  cityId: string
  cityName: string
}

export function listTechnicians() {
  return http.get<TechnicianDto[]>('/technicians')
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
