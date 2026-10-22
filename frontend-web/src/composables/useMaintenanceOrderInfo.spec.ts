import { describe, expect, it } from 'vitest'
import {
  formatPlace,
  formatScheduledDate,
  maintenanceIncludesLabels,
  maintenanceStatusLabel
} from './useMaintenanceOrderInfo'

describe('maintenanceStatusLabel', () => {
  it.each([
    ['Pendiente', 'Pendiente'],
    ['Asignada', 'Asignada'],
    ['EnProceso', 'En proceso'],
    ['Completada', 'Completada'],
    ['Cancelada', 'Cancelada']
  ])('%s -> %s', (status, label) => {
    expect(maintenanceStatusLabel(status)).toBe(label)
  })

  it('un estado desconocido se muestra tal cual, sin heredar del prototipo', () => {
    expect(maintenanceStatusLabel('Reprogramada')).toBe('Reprogramada')
    expect(maintenanceStatusLabel('constructor')).toBe('constructor')
  })
})

describe('maintenanceIncludesLabels', () => {
  const none = { includesGeneral: false, includesUnits: false, includesConsumables: false }

  it('lista solo lo marcado, en orden general / unidades / insumos', () => {
    expect(maintenanceIncludesLabels({ ...none, includesGeneral: true })).toEqual(['Mantenimiento general'])
    expect(maintenanceIncludesLabels({ ...none, includesUnits: true, includesConsumables: true })).toEqual([
      'Unidades',
      'Insumos'
    ])
    expect(
      maintenanceIncludesLabels({ includesGeneral: true, includesUnits: true, includesConsumables: true })
    ).toEqual(['Mantenimiento general', 'Unidades', 'Insumos'])
  })

  it('devuelve vacío si no incluye nada', () => {
    expect(maintenanceIncludesLabels(none)).toEqual([])
  })
})

describe('formatScheduledDate', () => {
  it('muestra un guion si no hay fecha o es inválida', () => {
    expect(formatScheduledDate(null)).toBe('—')
    expect(formatScheduledDate(undefined)).toBe('—')
    expect(formatScheduledDate('no-es-fecha')).toBe('—')
  })

  it('formatea una fecha válida con el año', () => {
    expect(formatScheduledDate('2026-03-15T12:00:00Z')).toMatch(/2026/)
  })
})

describe('formatPlace', () => {
  it('une sede y ciudad, omitiendo la que falte', () => {
    expect(formatPlace('Sede Norte', 'Bogotá')).toBe('Sede Norte · Bogotá')
    expect(formatPlace('Sede Norte', null)).toBe('Sede Norte')
    expect(formatPlace(null, 'Bogotá')).toBe('Bogotá')
    expect(formatPlace(null, null)).toBe('—')
  })
})
