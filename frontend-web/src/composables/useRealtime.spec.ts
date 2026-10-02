import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { effectScope } from 'vue'

type Fn = (...args: any[]) => void

// Doble mínimo de HubConnection: guarda los callbacks que registra useRealtime para poder dispararlos a mano.
class FakeConnection {
  static instances: FakeConnection[] = []
  on = vi.fn((name: string, fn: Fn) => {
    this.listeners[name] = fn
  })
  onreconnected = vi.fn((fn: Fn) => {
    this.reconnected = fn
  })
  onclose = vi.fn((fn: Fn) => {
    this.closed = fn
  })
  start = vi.fn(() => Promise.resolve())
  stop = vi.fn(() => Promise.resolve())
  listeners: Record<string, Fn> = {}
  reconnected: Fn = () => {}
  closed: Fn = () => {}
  constructor() {
    FakeConnection.instances.push(this)
  }
}

vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Disconnected: 'Disconnected' },
  LogLevel: { Warning: 3 },
  HubConnectionBuilder: class {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    configureLogging() {
      return this
    }
    build() {
      return new FakeConnection()
    }
  },
}))

vi.mock('../stores/auth', () => ({
  useAuthStore: () => ({ isAuthenticated: true, token: 'jwt', logout: vi.fn() }),
}))

const domListeners: Record<string, Fn> = {}
const fakeDocument = {
  visibilityState: 'visible',
  addEventListener: (name: string, fn: Fn) => {
    domListeners[name] = fn
  },
}
const fakeWindow = {
  location: { href: '' },
  addEventListener: (name: string, fn: Fn) => {
    domListeners[name] = fn
  },
}

async function loadModule() {
  vi.resetModules()
  FakeConnection.instances = []
  fakeDocument.visibilityState = 'visible'
  vi.stubGlobal('document', fakeDocument)
  vi.stubGlobal('window', fakeWindow)
  vi.stubEnv('VITE_API_URL', 'http://localhost:5000/api')
  return await import('./useRealtime')
}

// Deja que se resuelvan las promesas de start() sin avanzar los timers de debounce.
const flush = async () => {
  await Promise.resolve()
  await Promise.resolve()
  await Promise.resolve()
}

describe('useRealtime', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })
  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllEnvs()
    vi.unstubAllGlobals()
  })

  it('solo entrega los avisos de las entidades a las que se suscribió', async () => {
    const { useRealtimeUpdates } = await loadModule()
    const tickets = vi.fn()
    const assets = vi.fn()
    effectScope().run(() => {
      useRealtimeUpdates(['Ticket'], tickets)
      useRealtimeUpdates(['Asset'], assets)
    })
    await flush()

    FakeConnection.instances[0].listeners.entityChanged({ entity: 'Ticket', id: 't1', action: 'updated' })
    vi.advanceTimersByTime(300)

    expect(tickets).toHaveBeenCalledTimes(1)
    expect(assets).not.toHaveBeenCalled()
  })

  it('usa una sola conexión aunque se suscriban varias vistas', async () => {
    const { useRealtimeUpdates } = await loadModule()
    effectScope().run(() => {
      useRealtimeUpdates(['Ticket'], vi.fn())
      useRealtimeUpdates(['Asset'], vi.fn())
      useRealtimeUpdates(['Client'], vi.fn())
    })
    await flush()

    expect(FakeConnection.instances).toHaveLength(1)
  })

  it('agrupa avisos repetidos del mismo recurso en una sola llamada', async () => {
    const { useRealtimeUpdates } = await loadModule()
    const handler = vi.fn()
    effectScope().run(() => useRealtimeUpdates(['Ticket'], handler))
    await flush()

    const { listeners } = FakeConnection.instances[0]
    listeners.entityChanged({ entity: 'Ticket', id: 't1', action: 'updated' })
    listeners.entityChanged({ entity: 'Ticket', id: 't1', action: 'updated' })
    listeners.entityChanged({ entity: 'Ticket', id: 't2', action: 'updated' })
    vi.advanceTimersByTime(300)

    expect(handler).toHaveBeenCalledTimes(2)
  })

  it('al reconectar manda resync a TODAS las vistas, sin importar su entidad', async () => {
    const { useRealtimeUpdates } = await loadModule()
    const tickets = vi.fn()
    const users = vi.fn()
    effectScope().run(() => {
      useRealtimeUpdates(['Ticket'], tickets)
      useRealtimeUpdates(['User'], users)
    })
    await flush()

    FakeConnection.instances[0].reconnected()
    vi.advanceTimersByTime(300)

    expect(tickets).toHaveBeenCalledWith(expect.objectContaining({ action: 'resync' }))
    expect(users).toHaveBeenCalledWith(expect.objectContaining({ action: 'resync' }))
  })

  it('si el canal se cierra del todo, vuelve a conectar y hace resync', async () => {
    const { useRealtimeUpdates } = await loadModule()
    const handler = vi.fn()
    effectScope().run(() => useRealtimeUpdates(['Ticket'], handler))
    await flush()

    FakeConnection.instances[0].closed()
    await vi.advanceTimersByTimeAsync(5100)
    await flush()
    await vi.advanceTimersByTimeAsync(300)

    expect(FakeConnection.instances).toHaveLength(2)
    expect(handler).toHaveBeenCalledWith(expect.objectContaining({ action: 'resync' }))
  })

  it('una vista desmontada deja de recibir avisos', async () => {
    const { useRealtimeUpdates } = await loadModule()
    const handler = vi.fn()
    const scope = effectScope()
    scope.run(() => useRealtimeUpdates(['Ticket'], handler))
    await flush()

    scope.stop()
    FakeConnection.instances[0].listeners.entityChanged({ entity: 'Ticket', id: 't1', action: 'updated' })
    vi.advanceTimersByTime(300)

    expect(handler).not.toHaveBeenCalled()
  })

  it('disconnectRealtime cierra la conexión y no la reabre sola', async () => {
    const { useRealtimeUpdates, disconnectRealtime } = await loadModule()
    effectScope().run(() => useRealtimeUpdates(['Ticket'], vi.fn()))
    await flush()
    const conn = FakeConnection.instances[0]

    disconnectRealtime()
    conn.closed()
    await vi.advanceTimersByTimeAsync(6000)

    expect(conn.stop).toHaveBeenCalled()
    expect(FakeConnection.instances).toHaveLength(1)
  })

  it('al volver a la pestaña tras un rato oculta, hace resync', async () => {
    const { useRealtimeUpdates } = await loadModule()
    const handler = vi.fn()
    effectScope().run(() => useRealtimeUpdates(['Ticket'], handler))
    await flush()

    fakeDocument.visibilityState = 'hidden'
    domListeners.visibilitychange()
    vi.advanceTimersByTime(20000)
    fakeDocument.visibilityState = 'visible'
    domListeners.visibilitychange()
    await flush()
    vi.advanceTimersByTime(300)

    expect(handler).toHaveBeenCalledWith(expect.objectContaining({ action: 'resync' }))
  })

  it('un cambio de pestaña breve no provoca resync', async () => {
    const { useRealtimeUpdates } = await loadModule()
    const handler = vi.fn()
    effectScope().run(() => useRealtimeUpdates(['Ticket'], handler))
    await flush()

    fakeDocument.visibilityState = 'hidden'
    domListeners.visibilitychange()
    vi.advanceTimersByTime(3000)
    fakeDocument.visibilityState = 'visible'
    domListeners.visibilitychange()
    await flush()
    vi.advanceTimersByTime(300)

    expect(handler).not.toHaveBeenCalled()
  })
})
