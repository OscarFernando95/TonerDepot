import { describe, expect, it } from 'vitest'
import { confirmButtonLabel, maintenanceStartedMessage, useMaintenanceChoice } from './useMaintenanceChoice'

describe('useMaintenanceChoice', () => {
  it('sin ningún tipo marcado no es válido (el servidor exige al menos uno)', () => {
    const { valid } = useMaintenanceChoice()
    expect(valid.value).toBe(false)
  })

  it.each(['includesGeneral', 'includesUnits', 'includesConsumables'] as const)('marcar %s lo vuelve válido', (field) => {
    const { form, valid } = useMaintenanceChoice()
    form[field] = true
    expect(valid.value).toBe(true)
  })

  it('el staff arranca con general marcado', () => {
    const { valid } = useMaintenanceChoice(true)
    expect(valid.value).toBe(true)
  })

  it('toChoice recorta el motivo y manda null si queda en blanco', () => {
    const { form, toChoice } = useMaintenanceChoice()
    form.includesUnits = true
    form.reason = '  Cambio de unidades  '
    expect(toChoice()).toEqual({ includesGeneral: false, includesUnits: true, includesConsumables: false, reason: 'Cambio de unidades' })

    form.reason = '   '
    expect(toChoice().reason).toBeNull()
  })

  it('reset vuelve al estado inicial para que un diálogo reabierto no arrastre lo anterior', () => {
    const { form, valid, reset } = useMaintenanceChoice()
    form.includesConsumables = true
    form.reason = 'algo'
    reset()
    expect(valid.value).toBe(false)
    expect(form.reason).toBe('')
  })
})

describe('useMaintenanceChoice: cuándo lo hará el técnico', () => {
  const inDays = (days: number) => new Date(Date.now() + days * 24 * 60 * 60 * 1000)

  it('por defecto es "ahora" y el request no lleva plannedAt', () => {
    const { form, scheduling, toChoice } = useMaintenanceChoice()
    form.includesGeneral = true
    expect(scheduling.value).toBe(false)
    expect('plannedAt' in toChoice()).toBe(false)
  })

  it('programarlo sin elegir fecha no es válido', () => {
    const { form, valid } = useMaintenanceChoice(true)
    form.when = 'later'
    expect(valid.value).toBe(false)
  })

  it('programarlo con una fecha futura es válido y manda plannedAt en ISO UTC', () => {
    const { form, valid, plannedError, toChoice } = useMaintenanceChoice(true)
    const date = inDays(3)
    form.when = 'later'
    form.plannedAt = date
    expect(valid.value).toBe(true)
    expect(plannedError.value).toBeNull()
    expect(toChoice().plannedAt).toBe(date.toISOString())
  })

  it('rechaza el pasado y lo que pasa de 365 días', () => {
    const { form, valid, plannedError } = useMaintenanceChoice(true)
    form.when = 'later'
    form.plannedAt = new Date(Date.now() - 60_000)
    expect(valid.value).toBe(false)
    expect(plannedError.value).toMatch(/futuras/)

    form.plannedAt = inDays(400)
    expect(valid.value).toBe(false)
    expect(plannedError.value).toMatch(/365/)
  })

  it('volver a "ahora" descarta la fecha y deja el request como antes', () => {
    const { form, valid, toChoice } = useMaintenanceChoice(true)
    form.when = 'later'
    form.plannedAt = inDays(2)
    form.when = 'now'
    expect(valid.value).toBe(true)
    expect(toChoice().plannedAt).toBeUndefined()
  })

  it('aunque programar sea válido, sin ningún tipo marcado sigue sin serlo', () => {
    const { form, valid } = useMaintenanceChoice()
    form.when = 'later'
    form.plannedAt = inDays(2)
    expect(valid.value).toBe(false)
  })

  it('reset vuelve a "ahora" y sin fecha', () => {
    const { form, reset } = useMaintenanceChoice()
    form.when = 'later'
    form.plannedAt = inDays(2)
    reset()
    expect(form.when).toBe('now')
    expect(form.plannedAt).toBeNull()
  })
})

describe('confirmButtonLabel', () => {
  it('con fecha el botón dice "Programar mantenimiento"; sin ella, el texto de siempre', () => {
    expect(confirmButtonLabel('Iniciar', true)).toBe('Programar mantenimiento')
    expect(confirmButtonLabel('Iniciar', false)).toBe('Iniciar')
  })
})

describe('maintenanceStartedMessage', () => {
  const now = new Date(2026, 9, 22, 9, 0)
  it('sin fecha es el mensaje de siempre', () => {
    expect(maintenanceStartedMessage(undefined, now)).toBe('Mantenimiento listo. Haz el check-in en Mi trabajo.')
    expect(maintenanceStartedMessage(null, now)).toBe('Mantenimiento listo. Haz el check-in en Mi trabajo.')
  })

  it('con fecha dice cuándo y que el cliente la verá', () => {
    const text = maintenanceStartedMessage(new Date(2026, 9, 30, 10, 0).toISOString(), now)
    expect(text).toMatch(/^Mantenimiento programado para .*30.*10:00\. El cliente verá la fecha\.$/)
  })
})
