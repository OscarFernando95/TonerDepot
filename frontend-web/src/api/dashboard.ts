import { http } from './http'

export interface MttrDto {
  averageResolutionHours: number | null
  resolvedTicketCount: number
}

export interface MaintenanceComplianceDto {
  windowDays: number
  onTimePercentage: number | null
  completedCount: number
  onTimeCount: number
}

export interface CityTicketBacklogDto {
  cityId: string
  cityName: string
  openCount: number
  unassignedCount: number
}

export interface TechnicianUtilizationDto {
  technicianId: string
  technicianName: string
  hoursLogged: number
  utilizationPercentage: number
}

export interface SlaPriorityComplianceDto {
  priority: string
  targetHours: number
  resolvedCount: number
  withinSlaCount: number
  compliancePercentage: number | null
}

export interface SlaComplianceDto {
  overallCompliancePercentage: number | null
  byPriority: SlaPriorityComplianceDto[]
}

export interface DashboardSummaryDto {
  periodDays: number
  mttr: MttrDto
  maintenanceCompliance: MaintenanceComplianceDto
  ticketsByCity: CityTicketBacklogDto[]
  technicianUtilization: TechnicianUtilizationDto[]
  slaCompliance: SlaComplianceDto
}

export function getDashboardSummary(periodDays: number) {
  return http.get<DashboardSummaryDto>('/dashboard/summary', { params: { days: periodDays } })
}
