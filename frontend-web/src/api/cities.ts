import { http } from './http'
import type { CityDto, CreateCityRequest } from './types'

export function listCities() {
  return http.get<CityDto[]>('/cities')
}

export function createCity(request: CreateCityRequest) {
  return http.post<CityDto>('/cities', request)
}
