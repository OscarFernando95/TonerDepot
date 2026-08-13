export const RoleNames = {
  Administrador: 'Administrador',
  Coordinador: 'Coordinador',
  Tecnico: 'Tecnico',
  Cliente: 'Cliente',
  Ventas: 'Ventas'
} as const

export type RoleName = (typeof RoleNames)[keyof typeof RoleNames]

export interface CurrentUser {
  id: string
  cedula: string
  email: string | null
  fullName: string
  role: RoleName
  clientId: string | null
  technicianId: string | null
  mustChangePassword: boolean
}

export interface LoginResult {
  succeeded: boolean
  token: string | null
  expiresAtUtc: string | null
  user: CurrentUser | null
}

export interface UserDto {
  id: string
  cedula: string
  email: string | null
  fullName: string
  phone: string | null
  address: string | null
  cityId: string | null
  cityName: string | null
  roleName: RoleName
  isActive: boolean
  mustChangePassword: boolean
  clientId: string | null
  technicianId: string | null
  createdAt: string
}

export interface CreateUserRequest {
  cedula: string
  email?: string | null
  fullName: string
  phone: string
  address: string
  cityId: string
  roleName: RoleName
  clientId?: string | null
}

export interface CityDto {
  id: string
  name: string
  stateOrProvince: string
}

export interface ClientDto {
  id: string
  name: string
  taxId: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  isActive: boolean
  locationCount: number
  createdAt: string
}

export interface CreateClientRequest {
  name: string
  taxId?: string | null
  contactName?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  // Al menos una sede es obligatoria — garantiza que todo cliente nace con dónde prestarle servicio.
  locations: CreateClientLocationRequest[]
}

// A propósito NO es `= CreateClientRequest`: a diferencia de crear, editar los datos de un cliente
// nunca toca sus sedes (eso sigue siendo un flujo aparte desde el detalle del cliente).
export interface UpdateClientRequest {
  name: string
  taxId?: string | null
  contactName?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
}

export interface ClientLocationDto {
  id: string
  clientId: string
  clientName: string
  cityId: string
  cityName: string
  name: string
  address: string
  contactName: string | null
  contactPhone: string | null
  isActive: boolean
  createdAt: string
}

export interface CreateClientLocationRequest {
  cityId: string
  name: string
  address: string
  contactName?: string | null
  contactPhone?: string | null
}

export type UpdateClientLocationRequest = CreateClientLocationRequest

export const AssetTypes = {
  Impresora: 'Impresora',
  ComputoEquipo: 'ComputoEquipo'
} as const

export type AssetTypeName = (typeof AssetTypes)[keyof typeof AssetTypes]

export const AssetLifecycleStatuses = {
  EnBodega: 'EnBodega',
  Instalado: 'Instalado',
  EnMantenimiento: 'EnMantenimiento',
  DadoDeBaja: 'DadoDeBaja',
  PendienteInstalacion: 'PendienteInstalacion'
} as const

export type AssetLifecycleStatusName = (typeof AssetLifecycleStatuses)[keyof typeof AssetLifecycleStatuses]

export const AssetLifecycleStatusLabels: Record<AssetLifecycleStatusName, string> = {
  EnBodega: 'En bodega',
  Instalado: 'Instalado',
  EnMantenimiento: 'En mantenimiento',
  DadoDeBaja: 'Dado de baja',
  PendienteInstalacion: 'Pendiente de instalar'
}

// Transiciones válidas espejo de AssetService.AllowedTransitions en el backend — solo para
// habilitar/deshabilitar opciones en el selector; el backend sigue siendo la autoridad real.
// PendienteInstalacion solo se alcanza automáticamente al vincular un activo a un contrato — el
// selector de "Cambiar estado" del frontend la excluye a propósito de las opciones manuales.
export const AssetAllowedTransitions: Record<AssetLifecycleStatusName, AssetLifecycleStatusName[]> = {
  EnBodega: ['Instalado', 'PendienteInstalacion', 'DadoDeBaja'],
  Instalado: ['EnMantenimiento', 'EnBodega', 'DadoDeBaja'],
  EnMantenimiento: ['Instalado', 'EnBodega', 'DadoDeBaja'],
  PendienteInstalacion: ['Instalado', 'EnBodega'],
  DadoDeBaja: []
}

export interface AssetBrandDto {
  id: string
  name: string
}

export interface CreateAssetBrandRequest {
  name: string
}

export interface AssetDto {
  id: string
  assetBrandId: string
  assetBrandName: string
  model: string
  serialNumber: string
  type: AssetTypeName
  lifecycleStatus: AssetLifecycleStatusName
  area: string | null
  currentClientLocationId: string | null
  currentClientLocationName: string | null
  currentClientId: string | null
  currentClientName: string | null
  cityName: string | null
  lastMeterReading: number | null
  createdAt: string
}

export interface CreateAssetRequest {
  assetBrandId: string
  model: string
  serialNumber: string
  type: AssetTypeName
}

export type UpdateAssetRequest = CreateAssetRequest

export interface ChangeAssetStatusRequest {
  newStatus: AssetLifecycleStatusName
  clientLocationId?: string | null
  area?: string | null
  notes?: string | null
}

export interface AssetStatusLogDto {
  id: string
  previousStatus: AssetLifecycleStatusName
  newStatus: AssetLifecycleStatusName
  changedAt: string
  changedByUserName: string | null
  notes: string | null
}

export interface MeterReadingDto {
  id: string
  assetId: string
  readingDate: string
  counterValue: number
  registeredByUserName: string | null
}

export interface CreateMeterReadingRequest {
  readingDate?: string | null
  counterValue: number
}

export const ContractStatuses = {
  Activo: 'Activo',
  Vencido: 'Vencido',
  Cancelado: 'Cancelado'
} as const

export type ContractStatusName = (typeof ContractStatuses)[keyof typeof ContractStatuses]

export interface ContractDto {
  id: string
  clientId: string
  clientName: string
  startDate: string
  endDate: string | null
  status: ContractStatusName
  includedPrintsPerMonth: number | null
  pricePerExtraPage: number | null
  notes: string | null
  assetCount: number
  createdAt: string
}

export interface CreateContractRequest {
  clientId: string
  startDate: string
  endDate?: string | null
  includedPrintsPerMonth?: number | null
  pricePerExtraPage?: number | null
  notes?: string | null
}

export interface UpdateContractRequest {
  startDate: string
  endDate?: string | null
  includedPrintsPerMonth?: number | null
  pricePerExtraPage?: number | null
  notes?: string | null
}

export interface ContractAssetDto {
  id: string
  contractId: string
  assetId: string
  assetBrandName: string
  assetModel: string
  assetSerialNumber: string
  startDate: string
  endDate: string | null
}

export interface AddContractAssetRequest {
  assetId: string
  clientLocationId: string
  startDate?: string | null
}

export const MaintenanceFrequencyTypes = {
  PorContador: 'PorContador',
  PorTiempo: 'PorTiempo'
} as const

export type MaintenanceFrequencyTypeName = (typeof MaintenanceFrequencyTypes)[keyof typeof MaintenanceFrequencyTypes]

export const MaintenanceOrderStatusLabels: Record<string, string> = {
  Pendiente: 'Pendiente',
  Asignada: 'Asignada',
  EnProceso: 'En proceso',
  Completada: 'Completada',
  Cancelada: 'Cancelada'
}

export interface MaintenanceScheduleDto {
  id: string
  assetId: string
  assetBrandName: string
  assetModel: string
  assetSerialNumber: string
  contractId: string | null
  frequencyType: MaintenanceFrequencyTypeName
  printThreshold: number | null
  timeIntervalDays: number | null
  lastExecutedAt: string | null
  lastExecutedCounter: number | null
  nextDueAt: string | null
  nextDueCounter: number | null
  isActive: boolean
  createdAt: string
}

export interface CreateMaintenanceScheduleRequest {
  assetId: string
  contractId?: string | null
  frequencyType: MaintenanceFrequencyTypeName
  printThreshold?: number | null
  timeIntervalDays?: number | null
}

export interface UpdateMaintenanceScheduleRequest {
  printThreshold?: number | null
  timeIntervalDays?: number | null
}

export interface MaintenanceOrderDto {
  id: string
  maintenanceScheduleId: string
  assetId: string
  assetBrandName: string
  assetModel: string
  assetSerialNumber: string
  status: string
  technicianId: string | null
  technicianName: string | null
  scheduledDate: string
  completedAt: string | null
  createdAt: string
}

export const ServiceTicketStatuses = {
  Abierto: 'Abierto',
  SinAsignar: 'SinAsignar',
  Asignado: 'Asignado',
  EnProceso: 'EnProceso',
  Resuelto: 'Resuelto',
  Cerrado: 'Cerrado',
  Cancelado: 'Cancelado'
} as const

export type ServiceTicketStatusName = (typeof ServiceTicketStatuses)[keyof typeof ServiceTicketStatuses]

export const ServiceTicketStatusLabels: Record<string, string> = {
  Abierto: 'Abierto',
  SinAsignar: 'Sin asignar',
  Asignado: 'Asignado',
  EnProceso: 'En proceso',
  Resuelto: 'Resuelto',
  Cerrado: 'Cerrado',
  Cancelado: 'Cancelado'
}

// Espejo de ServiceTicketService.AllowedTransitions en el backend (Asignado se alcanza solo vía /assign).
export const ServiceTicketAllowedTransitions: Record<string, ServiceTicketStatusName[]> = {
  Abierto: ['Cancelado'],
  SinAsignar: ['Cancelado'],
  Asignado: ['EnProceso', 'Cancelado'],
  EnProceso: ['Resuelto', 'Cancelado'],
  Resuelto: ['Cerrado', 'EnProceso'],
  Cerrado: [],
  Cancelado: []
}

export const ServiceTicketPriorities = {
  Baja: 'Baja',
  Media: 'Media',
  Alta: 'Alta',
  Critica: 'Critica'
} as const

export type ServiceTicketPriorityName = (typeof ServiceTicketPriorities)[keyof typeof ServiceTicketPriorities]

export interface ServiceTicketDto {
  id: string
  clientLocationId: string
  clientLocationName: string
  clientId: string
  clientName: string
  assetId: string | null
  assetBrandName: string | null
  assetModel: string | null
  assetSerialNumber: string | null
  reportedByUserId: string
  reportedByUserName: string
  description: string
  status: ServiceTicketStatusName
  priority: ServiceTicketPriorityName
  technicianId: string | null
  technicianName: string | null
  resolvedAt: string | null
  closedAt: string | null
  createdAt: string
}

export interface CreateServiceTicketRequest {
  clientLocationId: string
  assetId?: string | null
  description: string
  priority?: ServiceTicketPriorityName | null
}

export interface AssignTicketRequest {
  technicianId: string
  reason?: string | null
}

export interface AssignmentHistoryDto {
  id: string
  technicianId: string | null
  technicianName: string | null
  assignedByUserName: string | null
  assignmentType: string
  reason: string | null
  assignedAt: string
}

export interface ProblemDetails {
  title: string
  status: number
  errors: Record<string, string[]> | null
}
