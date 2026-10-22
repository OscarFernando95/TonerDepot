// Presentación de una orden de mantenimiento para el portal del Cliente (solo lectura): etiqueta legible del
// estado, "qué incluye" y fecha programada. Funciones puras, sin estado, para poder probarlas sin montar la vista.
import { MaintenanceOrderStatusLabels } from '../api/types'

export interface MaintenanceIncludes {
  includesGeneral: boolean
  includesUnits: boolean
  includesConsumables: boolean
}

// Instanciar Intl.DateTimeFormat es caro y esto se llama una vez por fila y por render: a nivel de módulo se crea una
// sola vez por sesión. La fecha programada es un instante real, así que se muestra en la zona horaria local.
const scheduledDateFormatter = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', year: 'numeric' })

export function maintenanceStatusLabel(status: string): string {
  // hasOwnProperty: un estado llamado "constructor" o "toString" no debe devolver la función heredada.
  return Object.prototype.hasOwnProperty.call(MaintenanceOrderStatusLabels, status)
    ? MaintenanceOrderStatusLabels[status]
    : status
}

// Lista de lo que cubre la visita, en el orden en que la describe el negocio. Vacía si ninguna casilla viene marcada.
export function maintenanceIncludesLabels(order: MaintenanceIncludes): string[] {
  const labels: string[] = []
  if (order.includesGeneral) labels.push('Mantenimiento general')
  if (order.includesUnits) labels.push('Unidades')
  if (order.includesConsumables) labels.push('Insumos')
  return labels
}

export function formatScheduledDate(value: string | null | undefined): string {
  if (!value) return '—'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : scheduledDateFormatter.format(date)
}

// "Sede · Ciudad", omitiendo la parte que falte.
export function formatPlace(locationName: string | null, cityName: string | null): string {
  return [locationName, cityName].filter(Boolean).join(' · ') || '—'
}
