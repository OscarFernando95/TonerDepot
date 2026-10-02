import { http } from './http'
import { getPaged } from './paging'

export interface TonerFilter {
  from?: string
  to?: string
  zoneId?: string
  clientId?: string
  brandId?: string
  modelId?: string
}

export interface TonerGroupRow {
  name: string
  machines: number
  totalUnits: number
  avgPagesPerUnit: number | null
}

export interface TonerSummary {
  from: string
  to: string
  totalUnits: number
  changedByTechnicianUnits: number
  deliveredToUserUnits: number
  machines: number
  measuredUnits: number
  avgPagesPerUnit: number | null
  monthly: { month: string; units: number }[]
  byClient: TonerGroupRow[]
  byZone: TonerGroupRow[]
  byModel: TonerGroupRow[]
}

export interface TonerMachineRow {
  assetId: string
  brand: string
  model: string
  serialNumber: string
  clientName: string | null
  locationName: string | null
  zoneName: string | null
  totalUnits: number
  changedByTechnicianUnits: number
  deliveredToUserUnits: number
  measuredUnits: number
  avgPagesPerUnit: number | null
  pagesInRange: number | null
  vsModelPercent: number | null
  lastEventAt: string | null
  lastCounter: number | null
}

function clean(filter: TonerFilter) {
  return Object.fromEntries(Object.entries(filter).filter(([, v]) => v)) as Record<string, string>
}

export function getSummary(filter: TonerFilter) {
  return http.get<TonerSummary>('/analytics/toner/summary', { params: clean(filter) })
}

export function listMachines(filter: TonerFilter, page: number, pageSize: number) {
  return getPaged<TonerMachineRow>('/analytics/toner/machines', { ...clean(filter), page, pageSize } as never)
}

export function exportCsv(filter: TonerFilter) {
  return http.get<Blob>('/analytics/toner/export', { params: clean(filter), responseType: 'blob' })
}
