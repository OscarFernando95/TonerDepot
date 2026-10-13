import { describe, expect, it } from 'vitest'
import { statusTagType, useStatusTag } from './useStatusTag'

// Caracterización de los switch que vivían copiados en cada vista (AUDIT.md H-24).
describe('statusTagType', () => {
  it.each([
    ['ticket', 'Resuelto', 'success'],
    ['ticket', 'Cerrado', 'success'],
    ['ticket', 'Cancelado', 'info'],
    ['ticket', 'Abierto', 'warning'],
    ['ticket', 'SinAsignar', 'warning'],
    ['ticket', 'EnProceso', 'primary'],
    ['ticketPriority', 'Critica', 'danger'],
    ['ticketPriority', 'Alta', 'warning'],
    ['ticketPriority', 'Media', 'info'],
    ['maintenanceOrder', 'Completada', 'success'],
    ['maintenanceOrder', 'Cancelada', 'info'],
    ['maintenanceOrder', 'Pendiente', 'warning'],
    ['maintenanceOrder', 'EnProceso', 'primary'],
    ['asset', 'Instalado', 'success'],
    ['asset', 'EnMantenimiento', 'warning'],
    ['asset', 'PendienteInstalacion', 'warning'],
    ['asset', 'DadoDeBaja', 'danger'],
    ['asset', 'EnBodega', 'info'],
    ['contract', 'Activo', 'success'],
    ['contract', 'Vencido', 'warning'],
    ['contract', 'Cancelado', 'danger'],
    ['technician', 'Disponible', 'success'],
    ['technician', 'Ocupado', 'warning'],
    ['technician', 'EnTransito', 'primary'],
    ['technician', 'Inactivo', 'info']
  ] as const)('%s / %s -> %s', (entity, status, expected) => {
    expect(statusTagType(entity, status)).toBe(expected)
  })

  it('un estado desconocido usa el color por defecto de la entidad, sin lanzar', () => {
    expect(statusTagType('asset', 'EstadoNuevoDelBackend')).toBe('info')
    expect(statusTagType('ticket', 'EstadoNuevoDelBackend')).toBe('primary')
  })

  it('no confunde nombres de Object.prototype con estados', () => {
    expect(statusTagType('asset', 'constructor')).toBe('info')
    expect(statusTagType('asset', 'toString')).toBe('info')
  })

  it('useStatusTag ata la entidad', () => {
    const { tagType } = useStatusTag('contract')
    expect(tagType('Cancelado')).toBe('danger')
  })
})
