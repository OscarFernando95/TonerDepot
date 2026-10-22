import { describe, expect, it } from 'vitest'
import { useMaintenanceChoice } from './useMaintenanceChoice'

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
