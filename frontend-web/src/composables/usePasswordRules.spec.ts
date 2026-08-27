import { describe, expect, it } from 'vitest'
import { isPasswordValid } from './usePasswordRules'

// Espejo del test de paridad backend: ChangePasswordRequestValidatorTests.cs
// (backend/tests/Toner.Application.Tests/Auth). Mismos casos en ambos stacks — si alguien sube o
// baja una regla en ChangePasswordRequestValidator.cs sin tocar acá, este test se pone rojo y
// avisa que el composable quedó desincronizado.
describe('usePasswordRules', () => {
  it.each([
    ['corta1A', '< 10 caracteres'],
    ['sinmayuscula1', 'sin mayúscula'],
    ['SINMINUSCULA1', 'sin minúscula'],
    ['SinNumeroAqui', 'sin número']
  ])('rechaza "%s" (%s)', (password) => {
    expect(isPasswordValid(password)).toBe(false)
  })

  it('acepta una contraseña que cumple longitud y mezcla, sin símbolo', () => {
    expect(isPasswordValid('Passw0rdSegura')).toBe(true)
  })
})
