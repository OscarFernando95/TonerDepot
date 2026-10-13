import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const { push } = vi.hoisted(() => ({ push: vi.fn() }))
vi.mock('../router', () => ({ default: { push } }))
vi.mock('../composables/useRealtime', () => ({ disconnectRealtime: vi.fn() }))

import { http } from './http'
import { useAuthStore } from '../stores/auth'

// Hace que la próxima petición falle con el status dado, sin red: el adaptador de axios rechaza como
// lo haría una respuesta HTTP de error, y así se ejercita el interceptor de respuesta real.
function failWith(status: number) {
  http.defaults.adapter = (config) =>
    Promise.reject(Object.assign(new Error(`HTTP ${status}`), { config, response: { status, data: {}, config } }))
}

// El entorno de vitest es node (sin DOM): el store de auth lee localStorage, así que se simula.
function stubLocalStorage(initial: Record<string, string>) {
  const data = new Map(Object.entries(initial))
  vi.stubGlobal('localStorage', {
    getItem: (k: string) => data.get(k) ?? null,
    setItem: (k: string, v: string) => void data.set(k, v),
    removeItem: (k: string) => void data.delete(k)
  })
}

describe('interceptor de respuesta de http', () => {
  beforeEach(() => {
    stubLocalStorage({ toner_token: 'jwt-de-prueba' })
    setActivePinia(createPinia())
    push.mockClear()
  })

  it('401 cierra la sesión y redirige a login', async () => {
    failWith(401)
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(true)

    await expect(http.get('/cualquier-cosa')).rejects.toBeDefined()

    expect(auth.isAuthenticated).toBe(false)
    expect(push).toHaveBeenCalledWith({ name: 'login' })
  })

  // AUDIT.md H-08/H-36: "contraseña actual incorrecta" ahora llega como 400. Si volviera a ser 401, el
  // usuario perdería la sesión por un error de tipeo y este test lo detectaría.
  it('400 de validación (p. ej. contraseña actual incorrecta) NO cierra la sesión', async () => {
    failWith(400)
    const auth = useAuthStore()

    await expect(http.post('/auth/change-password', {})).rejects.toBeDefined()

    expect(auth.isAuthenticated).toBe(true)
    expect(push).not.toHaveBeenCalled()
  })

  it('403 y 500 tampoco cierran la sesión', async () => {
    const auth = useAuthStore()
    for (const status of [403, 500]) {
      failWith(status)
      await expect(http.get('/x')).rejects.toBeDefined()
    }
    expect(auth.isAuthenticated).toBe(true)
    expect(push).not.toHaveBeenCalled()
  })
})
