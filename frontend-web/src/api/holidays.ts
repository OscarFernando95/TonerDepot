import { http } from './http'

export interface HolidayDto {
  // yyyy-MM-dd
  date: string
  name: string
  // legal = calculado (Colombia); custom = ajuste de la empresa
  source: 'legal' | 'custom'
  // true solo cuando un ajuste fuerza laborable un festivo legal
  isWorkingDay: boolean
}

export function listHolidays(year: number) {
  return http.get<HolidayDto[]>('/holidays', { params: { year } })
}

export function setHolidayOverride(date: string, name: string, isWorkingDay: boolean) {
  return http.put<HolidayDto>('/holidays/overrides', { date, name, isWorkingDay })
}

export function removeHolidayOverride(date: string) {
  return http.delete(`/holidays/overrides/${date}`)
}
