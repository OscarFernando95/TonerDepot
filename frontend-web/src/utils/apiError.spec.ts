import { describe, expect, it } from 'vitest'
import { apiErrorMessage } from './apiError'

describe('apiErrorMessage', () => {
  it('400 de validación: usa los mensajes por campo, no el título genérico', () => {
    const err = {
      response: {
        status: 400,
        data: { title: 'Uno o más campos no son válidos.', errors: { Area: ['El área es obligatoria para completar la instalación.'] } }
      }
    }
    expect(apiErrorMessage(err, 'x')).toBe('El área es obligatoria para completar la instalación.')
  })

  it('varios campos: los une', () => {
    const err = { response: { data: { title: 't', errors: { A: ['uno'], B: ['dos', 'tres'] } } } }
    expect(apiErrorMessage(err, 'x')).toBe('uno dos tres')
  })

  it('409 u otro error con título: usa el título', () => {
    const err = { response: { status: 409, data: { title: 'Ya tienes una visita en curso.', errors: null } } }
    expect(apiErrorMessage(err, 'x')).toBe('Ya tienes una visita en curso.')
  })

  it('sin respuesta (red caída): usa el texto de respaldo', () => {
    expect(apiErrorMessage(new Error('Network Error'), 'No se pudo hacer check-out.')).toBe('No se pudo hacer check-out.')
  })
})
