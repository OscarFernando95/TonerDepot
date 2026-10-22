import { describe, expect, it } from 'vitest'
import {
  DEFAULT_WARNING_PRINTS,
  formatPrints,
  rentalRetirementDetail,
  rentalRetirementDisplay,
  validateRentalRetirement
} from './useRentalRetirement'

describe('rentalRetirementDisplay', () => {
  it.each([
    [null],
    [undefined],
    [{ rentalRetirementStatus: null, printsUntilRetirement: 1000 }],
    [{ printsUntilRetirement: 1000 }],
    [{ rentalRetirementStatus: 'Otro' as never, printsUntilRetirement: 1000 }]
  ])('no muestra nada sin estado conocido (%j)', (subject) => {
    expect(rentalRetirementDisplay(subject)).toBeNull()
  })

  it('Proxima: etiqueta ámbar con las impresiones que faltan', () => {
    const d = rentalRetirementDisplay({ rentalRetirementStatus: 'Proxima', printsUntilRetirement: 12345 })
    expect(d).toEqual({
      status: 'Proxima',
      label: 'Próxima a salir de alquiler',
      detail: `Faltan ${formatPrints(12345)} impresiones`,
      tagType: 'warning'
    })
  })

  it('Superada: etiqueta roja con lo que se pasó (valor negativo)', () => {
    const d = rentalRetirementDisplay({ rentalRetirementStatus: 'Superada', printsUntilRetirement: -2500 })
    expect(d?.label).toBe('Superó el límite de alquiler')
    expect(d?.tagType).toBe('danger')
    expect(d?.detail).toBe(`Superó el límite por ${formatPrints(2500)} impresiones`)
  })

  it('sin conteo de impresiones deja el detalle vacío pero muestra la etiqueta', () => {
    const d = rentalRetirementDisplay({ rentalRetirementStatus: 'Proxima', printsUntilRetirement: null })
    expect(d?.label).toBe('Próxima a salir de alquiler')
    expect(d?.detail).toBe('')
  })
})

describe('rentalRetirementDetail', () => {
  it('singular y plural', () => {
    expect(rentalRetirementDetail('Proxima', 1)).toBe('Falta 1 impresión')
    expect(rentalRetirementDetail('Proxima', 2)).toBe('Faltan 2 impresiones')
    expect(rentalRetirementDetail('Superada', -1)).toBe('Superó el límite por 1 impresión')
  })

  it('Superada justo en el límite', () => {
    expect(rentalRetirementDetail('Superada', 0)).toBe('Alcanzó el límite de impresiones')
  })

  it('Proxima nunca muestra un conteo negativo', () => {
    expect(rentalRetirementDetail('Proxima', -5)).toBe('Faltan 0 impresiones')
  })
})

describe('formatPrints', () => {
  it('usa separador de miles es-CO', () => {
    expect(formatPrints(50000)).toBe(new Intl.NumberFormat('es-CO').format(50000))
    expect(formatPrints(1234567)).toContain('234')
  })
})

describe('validateRentalRetirement', () => {
  it('acepta sin límite y con aviso por defecto', () => {
    expect(validateRentalRetirement(null, DEFAULT_WARNING_PRINTS)).toBeNull()
    expect(validateRentalRetirement(undefined, DEFAULT_WARNING_PRINTS)).toBeNull()
  })

  it('acepta un límite entero positivo', () => {
    expect(validateRentalRetirement(500000, 50000)).toBeNull()
  })

  it.each([[0], [-1], [1.5], [Number.NaN]])('rechaza un límite inválido (%s)', (v) => {
    expect(validateRentalRetirement(v, 50000)).not.toBeNull()
  })

  it.each([[0], [-10], [2.5], [null], [undefined]])('rechaza un aviso previo inválido (%s)', (v) => {
    expect(validateRentalRetirement(1000, v as never)).not.toBeNull()
  })
})
