import { describe, expect, it } from 'vitest'
import {
  MAX_PLANNED_DAYS_AHEAD,
  canScheduleOrder,
  disabledPlannedHours,
  disabledPlannedMinutes,
  formatPlannedAt,
  isOrderOpen,
  isPlannedDateDisabled,
  plannedAtLabel,
  toPlannedIso,
  validatePlannedAt
} from './useMaintenanceSchedule'

// Fechas locales fijas (sin Z) para que las pruebas no dependan de la zona horaria de la máquina.
const NOW = new Date(2026, 9, 22, 14, 30) // 22 oct 2026, 14:30 local

describe('formatPlannedAt', () => {
  it('muestra un guion si no hay fecha o es inválida', () => {
    expect(formatPlannedAt(null, NOW)).toBe('—')
    expect(formatPlannedAt(undefined, NOW)).toBe('—')
    expect(formatPlannedAt('no-es-fecha', NOW)).toBe('—')
  })

  it('formatea día, mes y hora local, sin año si es el año en curso', () => {
    const text = formatPlannedAt(new Date(2026, 9, 30, 10, 0), NOW)
    expect(text).toMatch(/30/)
    expect(text).toMatch(/oct/i)
    expect(text).toMatch(/10:00/)
    expect(text).not.toMatch(/2026/)
  })

  it('incluye el año si es de otro año', () => {
    expect(formatPlannedAt(new Date(2027, 0, 5, 9, 15), NOW)).toMatch(/2027/)
  })
})

describe('plannedAtLabel', () => {
  it('con fecha, dice "Programado para ..."', () => {
    expect(plannedAtLabel(new Date(2026, 9, 30, 10, 0).toISOString(), 'Asignada', NOW)).toMatch(/^Programado para .*10:00/)
  })

  it('sin fecha y orden abierta, avisa que aún no hay', () => {
    expect(plannedAtLabel(null, 'Pendiente', NOW)).toBe('Aún sin fecha programada')
    expect(plannedAtLabel(null, 'Asignada', NOW)).toBe('Aún sin fecha programada')
    expect(plannedAtLabel(null, 'EnProceso', NOW)).toBe('Aún sin fecha programada')
  })

  it('sin fecha y orden cerrada, no dice nada', () => {
    expect(plannedAtLabel(null, 'Completada', NOW)).toBe('')
    expect(plannedAtLabel(null, 'Cancelada', NOW)).toBe('')
  })

  it('con fecha inválida se trata como sin fecha', () => {
    expect(plannedAtLabel('basura', 'Asignada', NOW)).toBe('Aún sin fecha programada')
  })
})

describe('isOrderOpen / canScheduleOrder', () => {
  it('abiertas son Pendiente, Asignada y EnProceso', () => {
    expect(['Pendiente', 'Asignada', 'EnProceso'].every(isOrderOpen)).toBe(true)
    expect(isOrderOpen('Completada')).toBe(false)
    expect(isOrderOpen('Cancelada')).toBe(false)
  })

  it('solo se programa una orden Asignada', () => {
    expect(canScheduleOrder({ status: 'Asignada' })).toBe(true)
    expect(canScheduleOrder({ status: 'EnProceso' })).toBe(false)
    expect(canScheduleOrder({ status: 'Pendiente' })).toBe(false)
    expect(canScheduleOrder({ status: 'Completada' })).toBe(false)
  })
})

describe('validatePlannedAt', () => {
  it('exige elegir una fecha', () => {
    expect(validatePlannedAt(null, NOW)).toMatch(/Elige/)
    expect(validatePlannedAt(new Date('x'), NOW)).toMatch(/Elige/)
  })

  it('rechaza el pasado y el instante actual', () => {
    expect(validatePlannedAt(new Date(2026, 9, 22, 9, 0), NOW)).toMatch(/futuras/)
    expect(validatePlannedAt(new Date(NOW), NOW)).toMatch(/futuras/)
  })

  it('rechaza más de 365 días hacia adelante', () => {
    const tooFar = new Date(NOW.getTime() + (MAX_PLANNED_DAYS_AHEAD * 24 * 60 * 60 * 1000) + 60000)
    expect(validatePlannedAt(tooFar, NOW)).toMatch(/365/)
  })

  it('acepta una fecha futura dentro del rango', () => {
    expect(validatePlannedAt(new Date(2026, 9, 30, 10, 0), NOW)).toBeNull()
    expect(validatePlannedAt(new Date(NOW.getTime() + 60000), NOW)).toBeNull()
  })
})

describe('toPlannedIso', () => {
  it('convierte la hora local elegida a ISO UTC', () => {
    const local = new Date(2026, 9, 30, 10, 0)
    const iso = toPlannedIso(local)
    expect(iso).toMatch(/Z$/)
    expect(new Date(iso).getTime()).toBe(local.getTime())
  })
})

describe('selector de fecha', () => {
  it('deshabilita días anteriores a hoy, pero no hoy', () => {
    expect(isPlannedDateDisabled(new Date(2026, 9, 21), NOW)).toBe(true)
    expect(isPlannedDateDisabled(new Date(2026, 9, 22), NOW)).toBe(false)
    expect(isPlannedDateDisabled(new Date(2026, 9, 23), NOW)).toBe(false)
  })

  it('deshabilita días más allá de 365 días', () => {
    expect(isPlannedDateDisabled(new Date(2027, 9, 22), NOW)).toBe(false) // ~365 días: el último día habilitado
    expect(isPlannedDateDisabled(new Date(2027, 9, 24), NOW)).toBe(true)
  })

  it('hoy deshabilita las horas ya pasadas; otros días no', () => {
    expect(disabledPlannedHours(new Date(2026, 9, 22, 18, 0), NOW)).toEqual(Array.from({ length: 14 }, (_, i) => i))
    expect(disabledPlannedHours(new Date(2026, 9, 23, 8, 0), NOW)).toEqual([])
    expect(disabledPlannedHours(null, NOW)).toEqual([])
  })

  it('hoy y en la hora en curso deshabilita los minutos ya pasados', () => {
    const today = new Date(2026, 9, 22, 14, 45)
    expect(disabledPlannedMinutes(14, today, NOW)).toHaveLength(31) // 0..30
    expect(disabledPlannedMinutes(15, today, NOW)).toEqual([])
    expect(disabledPlannedMinutes(14, new Date(2026, 9, 23, 14, 0), NOW)).toEqual([])
  })
})
