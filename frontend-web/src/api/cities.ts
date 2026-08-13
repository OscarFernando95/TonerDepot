import { http } from './http'
import type { CityDto } from './types'

// Solo lectura: las ciudades son un catálogo fijo (municipios de Colombia con su departamento),
// sembrado en el backend — ya no hay forma de crear ciudades desde la app.
export function listCities() {
  return http.get<CityDto[]>('/cities')
}
