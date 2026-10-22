// Lógica de presentación del inicio del Cliente (ClientHome.vue): etiquetas, frases descriptivas, tono de cada tarjeta
// y el reparto "en orden vs. atención" de sus máquinas. Funciones puras y sin estado, para probarlas sin montar la vista.
// Nada de contratos ni de límites de alquiler: el backend no los envía y aquí tampoco se infieren.
import {
  ServiceTicketStatusLabels,
  type ClientHomeDto,
  type ClientHomeMaintenanceDto,
  type ClientHomeTicketDto
} from '../api/types'
import { maintenanceStatusLabel } from './useMaintenanceOrderInfo'
import { formatPlannedAt } from './useMaintenanceSchedule'

// Tono de acento de una tarjeta: calmado cuando todo está bien, ámbar o rojo suave cuando hay algo que atender.
export type HomeTone = 'calm' | 'info' | 'attention' | 'alert'
// Mismos estados que LaneStatus (lámpara + texto: nunca color solo).
export type LampState = 'on-time' | 'warning' | 'critical' | 'idle'

export const HOME_MAX_TICKETS = 3
export const HOME_MAX_MAINTENANCE = 3
export const HOME_MAX_INSTALLATIONS = 3

export function plural(n: number, one: string, many: string): string {
  return n === 1 ? one : many
}

// --- Saludo -----------------------------------------------------------------------------------------------------

export function greeting(hour: number): string {
  if (hour < 12) return 'Buenos días'
  if (hour < 19) return 'Buenas tardes'
  return 'Buenas noches'
}

// Una frase con lo que está en marcha, o la confirmación de que no hay nada pendiente.
export function homeSummary(home: ClientHomeDto): string {
  const parts: string[] = []
  if (home.openTickets > 0) parts.push(`${home.openTickets} ${plural(home.openTickets, 'ticket abierto', 'tickets abiertos')}`)
  if (home.openMaintenance > 0) {
    parts.push(`${home.openMaintenance} ${plural(home.openMaintenance, 'mantenimiento', 'mantenimientos')}`)
  }
  if (home.pendingInstallations > 0) {
    parts.push(`${home.pendingInstallations} ${plural(home.pendingInstallations, 'instalación pendiente', 'instalaciones pendientes')}`)
  }
  if (parts.length === 0) return 'Todo en orden: no tienes nada pendiente en este momento.'
  const last = parts.pop() as string
  return `Ahora mismo tienes ${parts.length ? `${parts.join(', ')} y ${last}` : last}.`
}

export function toneLamp(tone: HomeTone): LampState {
  if (tone === 'alert') return 'critical'
  if (tone === 'attention') return 'warning'
  return 'on-time'
}

export function toneLabel(tone: HomeTone): string {
  if (tone === 'alert') return 'Requiere atención'
  if (tone === 'attention') return 'Pendiente'
  if (tone === 'info') return 'En marcha'
  return 'En orden'
}

// --- Tiempo -----------------------------------------------------------------------------------------------------

// "hace 5 min", "hace 3 h", "hace 2 días". `now` se inyecta para poder probarlo.
export function ageLabel(since: string, now: number = Date.now()): string {
  const time = new Date(since).getTime()
  if (Number.isNaN(time)) return ''
  const mins = Math.floor((now - time) / 60000)
  if (mins < 1) return 'ahora'
  if (mins < 60) return `hace ${mins} min`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `hace ${hours} h`
  const days = Math.floor(hours / 24)
  return days === 1 ? 'hace 1 día' : `hace ${days} días`
}

// Hasta `max` elementos y cuántos quedaron fuera (para el "+N más").
export function splitVisible<T>(items: readonly T[], max: number): { shown: T[]; hidden: number } {
  return { shown: items.slice(0, max), hidden: Math.max(0, items.length - max) }
}

// --- Tickets ----------------------------------------------------------------------------------------------------

export function ticketStatusLabel(status: string): string {
  return Object.prototype.hasOwnProperty.call(ServiceTicketStatusLabels, status) ? ServiceTicketStatusLabels[status] : status
}

export function ticketStatusLamp(status: string): LampState {
  switch (status) {
    case 'Abierto':
    case 'SinAsignar':
      return 'warning'
    case 'Asignado':
    case 'EnProceso':
    case 'Resuelto':
    case 'Cerrado':
      return 'on-time'
    default:
      return 'idle'
  }
}

export function priorityLabel(priority: string): string {
  return priority === 'Critica' ? 'Crítica' : priority
}

export function priorityLamp(priority: string): LampState {
  if (priority === 'Critica') return 'critical'
  if (priority === 'Alta') return 'warning'
  return 'idle'
}

// Un ticket que aún espera técnico (no se ha empezado a atender).
function isWaiting(ticket: Pick<ClientHomeTicketDto, 'status'>): boolean {
  return ticket.status === 'Abierto' || ticket.status === 'SinAsignar'
}

export interface TicketsTile {
  tone: HomeTone
  value: number
  inProgress: number
  empty: boolean
  phrase: string
}

export function ticketsTile(home: ClientHomeDto): TicketsTile {
  const open = Math.max(0, home.openTickets)
  const inProgress = Math.min(Math.max(0, home.ticketsInProgress), open)
  if (open === 0) {
    return { tone: 'calm', value: 0, inProgress: 0, empty: true, phrase: 'Todo en orden: no tienes tickets abiertos.' }
  }

  const waiting = home.recentTickets.filter(isWaiting)
  let tone: HomeTone = inProgress > 0 ? 'info' : 'attention'
  if (waiting.some((t) => t.priority === 'Critica')) tone = 'alert'
  else if (waiting.some((t) => t.priority === 'Alta')) tone = 'attention'

  let phrase: string
  if (inProgress === 0) {
    phrase = 'Ninguno está en curso todavía; en cuanto un técnico empiece, lo verás aquí.'
  } else if (inProgress === open) {
    phrase = open === 1 ? 'Un técnico está atendiendo tu ticket ahora.' : 'Tus técnicos están atendiendo todos tus tickets.'
  } else {
    phrase = `${inProgress} en curso; el resto espera atención.`
  }
  return { tone, value: open, inProgress, empty: false, phrase }
}

// --- Mantenimientos ---------------------------------------------------------------------------------------------

export function maintenanceStatusLamp(status: string): LampState {
  return status === 'EnProceso' ? 'on-time' : 'idle'
}

export { maintenanceStatusLabel }

// "Visita: jue, 30 oct, 10:00" en hora local, o "Fecha por confirmar" mientras el técnico no la fije.
export function visitLabel(item: Pick<ClientHomeMaintenanceDto, 'plannedAt'>, now: Date = new Date()): string {
  return item.plannedAt && formatPlannedAt(item.plannedAt, now) !== '—'
    ? `Visita: ${formatPlannedAt(item.plannedAt, now)}`
    : 'Fecha por confirmar'
}

// Aviso suave si hay comentarios en los hilos de los mantenimientos que se muestran; null si no hay.
export function commentsNotice(items: readonly Pick<ClientHomeMaintenanceDto, 'commentCount'>[]): string | null {
  const total = items.reduce((sum, m) => sum + Math.max(0, m.commentCount), 0)
  if (total === 0) return null
  return total === 1 ? 'Hay 1 comentario en tus mantenimientos.' : `Hay ${total} comentarios en tus mantenimientos.`
}

export interface MaintenanceTile {
  tone: HomeTone
  scheduled: number
  inProgress: number
  withoutDate: number
  empty: boolean
  phrase: string
}

export function maintenanceTile(home: ClientHomeDto): MaintenanceTile {
  const scheduled = Math.max(0, home.maintenanceScheduled)
  const inProgress = Math.max(0, home.maintenanceInProgress)
  const withoutDate = Math.max(0, home.maintenanceWithoutVisitDate)
  if (home.openMaintenance <= 0 && scheduled + inProgress + withoutDate === 0) {
    return {
      tone: 'calm',
      scheduled: 0,
      inProgress: 0,
      withoutDate: 0,
      empty: true,
      phrase: 'Sin mantenimientos pendientes. Te avisaremos cuando se programe el próximo.'
    }
  }

  let phrase: string
  if (inProgress > 0) {
    phrase = inProgress === 1 ? 'Un técnico está haciendo el mantenimiento ahora.' : `Hay ${inProgress} mantenimientos en curso ahora.`
  } else if (scheduled > 0) {
    phrase = scheduled === 1 ? 'Tu próxima visita ya tiene fecha.' : `Tienes ${scheduled} visitas con fecha.`
  } else {
    phrase = 'Estamos coordinando la fecha de la visita.'
  }
  return { tone: 'info', scheduled, inProgress, withoutDate, empty: false, phrase }
}

// --- Instalaciones ----------------------------------------------------------------------------------------------

export interface InstallationsTile {
  tone: HomeTone
  pending: number
  inProgress: number
  empty: boolean
  phrase: string
}

export function installationsTile(home: ClientHomeDto): InstallationsTile {
  const pending = Math.max(0, home.pendingInstallations)
  const inProgress = Math.min(Math.max(0, home.installationsInProgress), pending)
  if (pending === 0 && home.installations.length === 0) {
    return { tone: 'calm', pending: 0, inProgress: 0, empty: true, phrase: 'Sin instalaciones pendientes' }
  }
  let phrase: string
  if (inProgress > 0) {
    phrase = inProgress === 1 ? 'Estamos instalando un equipo ahora.' : `Estamos instalando ${inProgress} equipos ahora.`
  } else {
    phrase = pending === 1 ? 'Tienes un equipo pendiente de instalación.' : `Tienes ${pending} equipos pendientes de instalación.`
  }
  return { tone: 'info', pending, inProgress, empty: false, phrase }
}

// --- Máquinas ---------------------------------------------------------------------------------------------------

export interface MachinesTile {
  tone: HomeTone
  installed: number
  inOrder: number
  attention: number
  inMaintenance: number
  // Porcentajes enteros que suman 100 (o 0/0 si no hay máquinas) para la barra.
  inOrderPct: number
  attentionPct: number
  empty: boolean
  phrase: string
}

// "En orden" = instaladas menos las que tienen un ticket abierto. Se acota por si los contadores llegaran
// desfasados (más tickets que máquinas): nunca sale un número negativo.
export function machinesTile(home: ClientHomeDto): MachinesTile {
  const installed = Math.max(0, home.installedAssets)
  const attention = Math.min(Math.max(0, home.assetsWithOpenTicket), installed)
  const inOrder = installed - attention
  const inMaintenance = Math.max(0, home.assetsInMaintenance)
  const inOrderPct = installed === 0 ? 0 : Math.round((inOrder / installed) * 100)
  const attentionPct = installed === 0 ? 0 : 100 - inOrderPct

  let phrase: string
  if (installed === 0) {
    phrase = 'Aún no tienes máquinas instaladas.'
  } else if (attention === 0) {
    phrase = installed === 1 ? 'Tu máquina está en orden.' : 'Todas tus máquinas están en orden.'
  } else {
    phrase = `${inOrder} de ${installed} en orden; ${attention} con ticket abierto.`
  }
  return {
    tone: attention > 0 ? 'attention' : 'calm',
    installed,
    inOrder,
    attention,
    inMaintenance,
    inOrderPct,
    attentionPct,
    empty: installed === 0,
    phrase
  }
}
