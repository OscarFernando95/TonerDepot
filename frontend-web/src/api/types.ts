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

// Solo la devuelven crear/resetear (nunca listar/actualizar): la contraseña generada, en claro, para
// mostrarla una vez en pantalla al Administrador (SECURITY_AUDIT.md hallazgo #3).
export interface UserWithGeneratedPasswordDto extends UserDto {
  generatedPassword: string
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

// Rol y cliente no son editables acá — ver comentario en UpdateUserRequest.cs del backend.
export interface UpdateUserRequest {
  cedula: string
  email?: string | null
  fullName: string
  phone: string
  address: string
  cityId: string
}

export interface CityDto {
  id: string
  name: string
  stateOrProvince: string
}

export type SupportCoverage = 'HorarioOficina' | 'Continuo24x7'

export const SupportCoverageLabels: Record<SupportCoverage, string> = {
  HorarioOficina: 'Horario de oficina',
  Continuo24x7: '24/7'
}

export interface ClientDto {
  id: string
  name: string
  taxId: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  isActive: boolean
  // true: cliente con contrato de alquiler (sus tickets se ligan a un Asset real). false: cliente
  // externo que pide servicio sobre equipos propios, a veces ni catalogados.
  isContractClient: boolean
  // HorarioOficina: SLA en horas hábiles y solo técnicos en horario. Continuo24x7: horas corridas y
  // asignación sin importar el horario del técnico (sí sus permisos).
  supportCoverage: SupportCoverage
  locationCount: number
  cityNames: string[]
  createdAt: string
}

export interface CreateClientRequest {
  name: string
  taxId?: string | null
  contactName?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  isContractClient: boolean
  supportCoverage: SupportCoverage
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
  isContractClient: boolean
  supportCoverage: SupportCoverage
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
  // Coordenadas WGS84 de la sede (para verificar que el técnico llegó); null si aún no se cargaron.
  latitude: number | null
  longitude: number | null
  isActive: boolean
  createdAt: string
}

export interface CreateClientLocationRequest {
  cityId: string
  name: string
  address: string
  contactName?: string | null
  contactPhone?: string | null
  latitude?: number | null
  longitude?: number | null
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

// Umbrales de mantenimiento del modelo: constantes por marca+modelo, editables a demanda. Reemplazan
// la antigua política por AssetType.
export interface AssetModelDto {
  id: string
  assetBrandId: string
  name: string
  generalPrintThreshold: number
  generalMonthsInterval: number
  unitsPrintThreshold: number
  unitsMonthsInterval: number
  consumablesPrintThreshold: number
}

export interface CreateAssetModelRequest {
  name: string
  generalPrintThreshold: number
  generalMonthsInterval: number
  unitsPrintThreshold: number
  unitsMonthsInterval: number
  consumablesPrintThreshold: number
}

export type UpdateAssetModelRequest = CreateAssetModelRequest

export interface AssetDto {
  id: string
  assetModelId: string
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
  activeContractId: string | null
  createdAt: string
}

export interface CreateAssetRequest {
  assetModelId: string
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
  cityNames: string[]
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
  area: string | null
  lastMeterReading: number | null
  averageMonthlyPrints: number | null
}

export interface AddContractAssetRequest {
  assetId: string
  clientLocationId: string
  startDate?: string | null
}

export const MaintenanceOrderStatusLabels: Record<string, string> = {
  Pendiente: 'Pendiente',
  Asignada: 'Asignada',
  EnProceso: 'En proceso',
  Completada: 'Completada',
  Cancelada: 'Cancelada'
}

// Un cronograma por activo, con tres sub-reglas independientes: mantenimiento general y mantenimiento
// de unidades (ambas híbridas contador/tiempo) y cambio de insumos (puro contador), evaluadas contra los
// umbrales de AssetModel (marca+modelo del activo). Se crea automáticamente al instalar un activo bajo
// un contrato — ver TechnicianSelfServiceController check-out de instalación.
export interface MaintenanceScheduleDto {
  id: string
  assetId: string
  assetBrandName: string
  assetModel: string
  assetSerialNumber: string

  contractId: string
  clientId: string
  clientName: string
  clientLocationName: string | null
  cityName: string | null
  area: string | null

  isActive: boolean
  lastKnownCounter: number | null

  // Resumen del mantenimiento más reciente (fecha + glosas MG/MU/CI de lo que se hizo en esa fecha).
  lastMaintenanceAt: string | null
  lastMaintenanceCodes: string[]

  // Predicción de "qué sigue" — fecha estimada (null si la regla líder es solo insumos, que es puro
  // contador), contador estimado, y glosas de qué incluiría.
  nextMaintenanceAt: string | null
  nextMaintenanceCounter: number
  nextMaintenanceCodes: string[]

  // Campos granulares por sub-regla — ya no se muestran como columnas, solo alimentan el tooltip.
  lastGeneralMaintenanceAt: string | null
  lastGeneralMaintenanceCounter: number | null
  nextGeneralDueAt: string
  nextGeneralDueCounter: number

  lastUnitsMaintenanceAt: string | null
  lastUnitsMaintenanceCounter: number | null
  nextUnitsDueAt: string
  nextUnitsDueCounter: number

  lastConsumablesChangeAt: string | null
  lastConsumablesChangeCounter: number | null
  nextConsumablesDueCounter: number

  createdAt: string
}

export interface CompleteMaintenanceOrderRequest {
  counterValue: number
  readingDate?: string | null
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
  clientLocationName: string | null
  cityName: string | null
  includesGeneral: boolean
  includesUnits: boolean
  includesConsumables: boolean
  scheduledDate: string
  completedAt: string | null
  createdAt: string
}

export interface MeterReadingAssetDto {
  assetId: string
  assetBrandName: string
  model: string
  serialNumber: string
  clientId: string | null
  clientName: string | null
  clientLocationName: string | null
  area: string | null
  cityName: string | null
  lastMeterReading: number | null
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
  cityName: string | null
  assetId: string | null
  assetBrandName: string | null
  assetModel: string | null
  assetSerialNumber: string | null
  // El activo tiene hoy un contrato vigente: la foto del contador es obligatoria solo en ese caso.
  assetUnderContract: boolean
  // Solo aplican cuando assetId es null (cliente externo, equipo sin catalogar) — las llena el técnico
  // de forma opcional al cerrar el ticket.
  externalAssetBrand: string | null
  externalAssetModel: string | null
  externalAssetCounter: number | null
  reportedByUserId: string
  reportedByUserName: string
  description: string
  status: ServiceTicketStatusName
  priority: ServiceTicketPriorityName
  technicianId: string | null
  technicianName: string | null
  resolvedAt: string | null
  closedAt: string | null
  // Solo vienen en el detalle y solo para staff/técnico (ver ServiceTicketDto.cs).
  resolutionNotes?: string | null
  resolutionDurationMinutes?: number | null
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
