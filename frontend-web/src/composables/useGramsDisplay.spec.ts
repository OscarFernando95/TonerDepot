import { describe, expect, it } from 'vitest'
import { formatGrams, formatQuantity } from './useGramsDisplay'

// Los separadores de es-CO usan espacio/punto según el ICU de Node; se normalizan para no atar el test al locale.
const norm = (s: string) => s.replace(/ /g, ' ')

describe('formatGrams', () => {
  it('en modo gramos muestra el saldo tal cual', () => {
    expect(norm(formatGrams(250, 500, 'g'))).toBe('250 g')
    expect(norm(formatGrams(1250, 500, 'g'))).toMatch(/^1\.?250 g$/)
  })

  it('en modo kg muestra kilos y bolsas completas + resto', () => {
    expect(norm(formatGrams(1250, 500, 'kg'))).toBe('1,25 kg (2 bolsas + 250 g)')
    expect(norm(formatGrams(500, 500, 'kg'))).toBe('0,5 kg (1 bolsa)')
    expect(norm(formatGrams(180, 500, 'kg'))).toBe('0,18 kg (180 g)')
  })

  it('en modo kg sin tamaño de bolsa solo muestra kilos', () => {
    expect(norm(formatGrams(750, null, 'kg'))).toBe('0,75 kg')
  })

  it('mantiene el signo en saldos negativos', () => {
    expect(norm(formatGrams(-180, 500, 'kg'))).toBe('-0,18 kg (-180 g)')
  })
})

describe('formatQuantity', () => {
  it('deja intacto lo que no es revelador', () => {
    expect(formatQuantity(7, null, 'kg')).toBe('7')
  })

  it('formatea el revelador según el modo', () => {
    expect(norm(formatQuantity(1000, 500, 'kg'))).toBe('1 kg (2 bolsas)')
  })
})
