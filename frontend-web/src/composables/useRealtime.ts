import { onScopeDispose } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../stores/auth'

export interface EntityChangedEvent {
  entity: string
  id: string
  action: 'created' | 'updated' | 'deleted' | 'resync'
}

type Handler = (event: EntityChangedEvent) => void
interface Subscription {
  entities: string[]
  handler: Handler
}

// Un solo canal por pestaña, compartido por todas las vistas. Cada vista se registra en `subscriptions`; el canal
// despacha desde ahí, así que no depende de on/off sobre una conexión que quizá aún no existe.
//
// Resiliencia: SignalR por defecto se rinde tras ~40 s sin red (pestaña dormida, laptop suspendida, cambio de wifi)
// y no vuelve. Aquí reintenta sin límite con backoff acotado, y cada vez que el canal vuelve (o la pestaña
// reaparece tras un rato oculta) manda un evento `resync` a TODAS las vistas: durante el corte se pudieron perder
// avisos, y la única forma segura de ponerse al día es volver a pedir los datos.
const subscriptions = new Set<Subscription>()
const RETRY_DELAYS_MS = [0, 2000, 5000, 10000, 30000]
const RESYNC_AFTER_HIDDEN_MS = 15000

let connection: signalR.HubConnection | null = null
let starting: Promise<void> | null = null
let hiddenAt: number | null = null
let listenersInstalled = false
let everConnected = false

// Un guardado puede tocar varias entidades a la vez: los avisos del mismo recurso se agrupan en una sola llamada al
// handler (ventana corta), para no disparar N recargas seguidas. Cada recurso distinto sigue teniendo la suya.
const DEBOUNCE_MS = 250
const timers = new Map<Subscription, Map<string, ReturnType<typeof setTimeout>>>()

function call(sub: Subscription, event: EntityChangedEvent) {
  try {
    sub.handler(event)
  } catch (err) {
    console.error('Error en un handler de tiempo real:', err)
  }
}

function dispatch(event: EntityChangedEvent) {
  const key = `${event.entity}:${event.id}:${event.action === 'resync' ? 'resync' : ''}`
  for (const sub of [...subscriptions]) {
    if (event.action !== 'resync' && !sub.entities.includes(event.entity)) continue

    let pending = timers.get(sub)
    if (!pending) timers.set(sub, (pending = new Map()))
    const previous = pending.get(key)
    if (previous) clearTimeout(previous)
    const mine = pending
    pending.set(
      key,
      setTimeout(() => {
        mine.delete(key)
        if (subscriptions.has(sub)) call(sub, event)
      }, DEBOUNCE_MS)
    )
  }
}

function resyncAll() {
  dispatch({ entity: '*', id: '', action: 'resync' })
}

function buildConnection(): signalR.HubConnection {
  const auth = useAuthStore()
  // VITE_API_URL termina en "/api" — el hub vive en la raíz del sitio, no bajo /api.
  const apiBaseUrl: string = import.meta.env.VITE_API_URL
  const siteBaseUrl = apiBaseUrl.endsWith('/api') ? apiBaseUrl.slice(0, -4) : apiBaseUrl

  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${siteBaseUrl}/hubs/updates`, { accessTokenFactory: () => auth.token ?? '' })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (ctx) => RETRY_DELAYS_MS[Math.min(ctx.previousRetryCount, RETRY_DELAYS_MS.length - 1)],
    })
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  conn.on('entityChanged', (event: EntityChangedEvent) => dispatch(event))

  // Si el servidor revoca la sesión (logout en otro dispositivo, cierre por administrador, cambio de contraseña),
  // se sale igual que con un 401 de la API (ver api/http.ts).
  conn.on('sessionRevoked', () => {
    disconnectRealtime()
    auth.logout()
    window.location.href = '/login'
  })

  conn.onreconnected(() => resyncAll())

  // Agotados los reintentos de la librería (o el primer start falló): se vuelve a intentar desde cero.
  conn.onclose(() => {
    if (connection === conn) {
      connection = null
      scheduleRestart()
    }
  })

  return conn
}

let restartTimer: ReturnType<typeof setTimeout> | null = null

function scheduleRestart() {
  if (restartTimer || subscriptions.size === 0 || !useAuthStore().isAuthenticated) return
  restartTimer = setTimeout(() => {
    restartTimer = null
    ensureConnected()
  }, 5000)
}

function ensureConnected(): Promise<void> {
  if (connection) return Promise.resolve()
  if (starting) return starting

  const auth = useAuthStore()
  if (!auth.isAuthenticated) return Promise.resolve()

  const conn = buildConnection()
  starting = conn
    .start()
    .then(() => {
      connection = conn
      // Reconexión tras un cierre total: pudo perderse todo lo ocurrido mientras no hubo canal.
      if (everConnected) resyncAll()
      everConnected = true
    })
    .catch((err) => {
      console.error('No se pudo conectar el canal en tiempo real:', err)
      scheduleRestart()
    })
    .finally(() => {
      starting = null
    })
  return starting
}

function installLifecycleListeners() {
  if (listenersInstalled) return
  listenersInstalled = true

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      hiddenAt = Date.now()
      return
    }
    const wasHiddenLong = hiddenAt !== null && Date.now() - hiddenAt > RESYNC_AFTER_HIDDEN_MS
    hiddenAt = null
    ensureConnected().then(() => {
      if (wasHiddenLong) resyncAll()
    })
  })

  window.addEventListener('online', () => {
    ensureConnected().then(resyncAll)
  })
}

// Cierra el canal (logout, sesión revocada). Debe llamarse al salir: si no, la conexión conserva los grupos del
// usuario anterior y el siguiente que inicie sesión en la pestaña recibiría (o dejaría de recibir) avisos ajenos.
export function disconnectRealtime() {
  if (restartTimer) {
    clearTimeout(restartTimer)
    restartTimer = null
  }
  const conn = connection
  connection = null
  everConnected = false
  if (conn) void conn.stop()
}

// Se suscribe a "algo cambió" para las entidades indicadas mientras el componente que llama esté montado. El
// handler recibe solo el id/acción — quien lo use vuelve a pedir el recurso por la API de siempre, nunca confía en
// datos que vengan por el canal. También recibe `action: 'resync'` (de cualquier entidad) cuando el canal volvió
// tras un corte: debe recargar todo lo que muestra, no solo un registro concreto.
export function useRealtimeUpdates(entities: string[], handler: Handler) {
  const sub: Subscription = { entities, handler }
  subscriptions.add(sub)
  installLifecycleListeners()
  void ensureConnected()

  onScopeDispose(() => {
    subscriptions.delete(sub)
    timers.get(sub)?.forEach((t) => clearTimeout(t))
    timers.delete(sub)
  })
}
