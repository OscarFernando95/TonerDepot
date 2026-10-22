// Retiro de alquiler por modelo: cuando el contador total de un equipo se acerca (o pasa) el límite de su
// modelo, el equipo debe salir de alquiler y pasar a venta. Es solo informativo: no bloquea ni dispara nada.
// El backend calcula el estado (`rentalRetirementStatus`) y las impresiones que faltan; aquí solo se dan
// etiquetas, textos y la validación del formulario del modelo, para no repetirlos en cada vista.

import type { RentalRetirementStatus } from '../api/types'

export const DEFAULT_WARNING_PRINTS = 50000

// A nivel de módulo: construir un Intl.NumberFormat por render es costoso.
const numberFormat = new Intl.NumberFormat('es-CO')

export function formatPrints(value: number): string {
  return numberFormat.format(value)
}

export interface RentalRetirementSubject {
  rentalRetirementStatus?: RentalRetirementStatus | null
  printsUntilRetirement?: number | null
}

export interface RentalRetirementDisplay {
  status: RentalRetirementStatus
  label: string
  // Texto bajo la etiqueta; '' si el backend no mandó el conteo de impresiones.
  detail: string
  tagType: 'warning' | 'danger'
}

const LABELS: Record<RentalRetirementStatus, string> = {
  Proxima: 'Próxima a salir de alquiler',
  Superada: 'Superó el límite de alquiler'
}

const TAG_TYPES: Record<RentalRetirementStatus, 'warning' | 'danger'> = {
  Proxima: 'warning',
  Superada: 'danger'
}

function plural(n: number, one: string, many: string) {
  return `${formatPrints(n)} ${n === 1 ? one : many}`
}

export function rentalRetirementDetail(status: RentalRetirementStatus, printsUntilRetirement: number | null | undefined): string {
  if (printsUntilRetirement == null) return ''
  if (status === 'Proxima') {
    const left = Math.max(printsUntilRetirement, 0)
    return `${left === 1 ? 'Falta' : 'Faltan'} ${plural(left, 'impresión', 'impresiones')}`
  }
  const over = Math.abs(printsUntilRetirement)
  return over === 0 ? 'Alcanzó el límite de impresiones' : `Superó el límite por ${plural(over, 'impresión', 'impresiones')}`
}

// null cuando no hay nada que mostrar (sin límite, sin lecturas, rol Cliente o un estado que este front no conoce).
export function rentalRetirementDisplay(subject: RentalRetirementSubject | null | undefined): RentalRetirementDisplay | null {
  const status = subject?.rentalRetirementStatus
  if (status !== 'Proxima' && status !== 'Superada') return null
  return {
    status,
    label: LABELS[status],
    detail: rentalRetirementDetail(status, subject?.printsUntilRetirement),
    tagType: TAG_TYPES[status]
  }
}

// Validación del formulario de parámetros del modelo (el backend la repite con FluentValidation).
// Devuelve el mensaje de error o null si los valores son válidos.
export function validateRentalRetirement(
  threshold: number | null | undefined,
  warningPrints: number | null | undefined
): string | null {
  if (threshold != null && (!Number.isInteger(threshold) || threshold <= 0)) {
    return 'Las impresiones para salir de alquiler deben ser un entero mayor que 0 (o vacío si no hay límite).'
  }
  if (warningPrints == null || !Number.isInteger(warningPrints) || warningPrints <= 0) {
    return 'El aviso previo debe ser un entero mayor que 0.'
  }
  return null
}

export function useRentalRetirement() {
  return { rentalRetirementDisplay, rentalRetirementDetail, formatPrints, validateRentalRetirement }
}
