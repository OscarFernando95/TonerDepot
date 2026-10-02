import { http } from './http'

export interface ZoneCityDto {
  id: string
  name: string
  stateOrProvince: string
}

export interface ZoneDto {
  id: string
  name: string
  cities: ZoneCityDto[]
  technicianCount: number
}

// Catálogo de pocas filas: no se pagina.
export function listZones() {
  return http.get<ZoneDto[]>('/zones')
}

export function createZone(name: string) {
  return http.post<ZoneDto>('/zones', { name })
}

export function renameZone(id: string, name: string) {
  return http.patch<ZoneDto>(`/zones/${id}`, { name })
}

// Conjunto COMPLETO de municipios de la zona: los que no estén quedan sin zona y los que estén en otra se mueven aquí.
export function setZoneCities(id: string, cityIds: string[]) {
  return http.put<ZoneDto>(`/zones/${id}/cities`, { cityIds })
}

export function deleteZone(id: string) {
  return http.delete(`/zones/${id}`)
}
