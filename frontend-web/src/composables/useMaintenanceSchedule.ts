// Programación de un mantenimiento: el técnico asignado fija cuándo lo hará y el cliente lo ve. Funciones puras
// (sin estado) para poder probarlas sin montar las vistas. Las reglas de validación reflejan las del servidor
// (fecha futura y a no más de 365 días); el servidor las exige igual, esto solo evita ir y volver con un error.

// Instanciar Intl.DateTimeFormat es caro y esto se llama una vez por fila y por render: a nivel de módulo se crea una
// sola vez por sesión. La fecha programada es un instante real, así que se muestra en la zona horaria local.
// "jue, 30 oct, 10:00" (sin año si es el año en curso) y la variante con año.
const plannedFormatter = new Intl.DateTimeFormat('es-CO', {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23'
})
const plannedFormatterWithYear = new Intl.DateTimeFormat('es-CO', {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23'
})

// Máximo de días hacia adelante que acepta el servidor para programar.
export const MAX_PLANNED_DAYS_AHEAD = 365
const DAY_MS = 24 * 60 * 60 * 1000

const OPEN_STATUSES = ['Pendiente', 'Asignada', 'EnProceso']

// Una orden "abierta" todavía puede cambiar; Completada y Cancelada ya son historia.
export function isOrderOpen(status: string): boolean {
  return OPEN_STATUSES.includes(status)
}

// El técnico solo puede (re)programar mientras la orden está Asignada: con la visita iniciada ya no tiene sentido.
export function canScheduleOrder(order: { status: string }): boolean {
  return order.status === 'Asignada'
}

function parse(value: string | Date | null | undefined): Date | null {
  if (!value) return null
  const date = value instanceof Date ? value : new Date(value)
  return Number.isNaN(date.getTime()) ? null : date
}

// "jue, 30 oct, 10:00" en hora local; con año si no es el año en curso. '—' si no hay fecha o es inválida.
export function formatPlannedAt(value: string | Date | null | undefined, now: Date = new Date()): string {
  const date = parse(value)
  if (!date) return '—'
  return (date.getFullYear() === now.getFullYear() ? plannedFormatter : plannedFormatterWithYear).format(date)
}

// Línea de estado de la fecha programada para el cliente. Vacía si la orden ya se cerró sin que se programara nada
// (decir "aún sin fecha" de algo cancelado sería confuso).
export function plannedAtLabel(plannedAt: string | null | undefined, status: string, now: Date = new Date()): string {
  if (parse(plannedAt)) return `Programado para ${formatPlannedAt(plannedAt, now)}`
  return isOrderOpen(status) ? 'Aún sin fecha programada' : ''
}

// Mensaje de error si la fecha elegida no sirve; null si es válida.
export function validatePlannedAt(value: Date | null | undefined, now: Date = new Date()): string | null {
  const date = parse(value)
  if (!date) return 'Elige la fecha y la hora.'
  if (date.getTime() <= now.getTime()) return 'La fecha y la hora deben ser futuras.'
  if (date.getTime() - now.getTime() > MAX_PLANNED_DAYS_AHEAD * DAY_MS) {
    return `La fecha no puede estar a más de ${MAX_PLANNED_DAYS_AHEAD} días.`
  }
  return null
}

// Instante ISO en UTC que espera el servidor, a partir de la fecha elegida en hora local.
export function toPlannedIso(value: Date): string {
  return value.toISOString()
}

function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}

// Días del selector que no se pueden elegir: antes de hoy o más allá del máximo. Por día calendario local.
export function isPlannedDateDisabled(day: Date, now: Date = new Date()): boolean {
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const lastDay = new Date(now.getTime() + MAX_PLANNED_DAYS_AHEAD * DAY_MS)
  const endOfLastDay = new Date(lastDay.getFullYear(), lastDay.getMonth(), lastDay.getDate() + 1)
  return day.getTime() < startOfToday.getTime() || day.getTime() >= endOfLastDay.getTime()
}

// Horas pasadas del día de hoy (el selector de hora no se entera de que "hoy" tiene pasado).
export function disabledPlannedHours(selected: Date | null | undefined, now: Date = new Date()): number[] {
  if (!selected || !sameDay(selected, now)) return []
  return Array.from({ length: now.getHours() }, (_, hour) => hour)
}

// Minutos pasados dentro de la hora en curso, solo si hoy y esa misma hora están elegidos.
export function disabledPlannedMinutes(hour: number, selected: Date | null | undefined, now: Date = new Date()): number[] {
  if (!selected || !sameDay(selected, now) || hour !== now.getHours()) return []
  return Array.from({ length: now.getMinutes() + 1 }, (_, minute) => minute)
}
