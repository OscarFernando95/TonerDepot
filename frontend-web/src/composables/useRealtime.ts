import { onScopeDispose } from 'vue'
import * as signalR from '@microsoft/signalr'
import { useAuthStore } from '../stores/auth'

export interface EntityChangedEvent {
  entity: string
  id: string
  action: 'created' | 'updated' | 'deleted'
}

// Un solo canal por sesión de pestaña, reutilizado por todas las vistas que se suscriben. Reconecta solo
// (backoff de la propia librería); si el servidor revoca la sesión (logout en otro dispositivo, cierre por
// administrador), redirige a login igual que un 401 de la API (ver api/http.ts).
let connection: signalR.HubConnection | null = null
let starting: Promise<signalR.HubConnection> | null = null

async function getConnection(): Promise<signalR.HubConnection> {
  if (connection) return connection
  if (starting) return starting

  const auth = useAuthStore()
  // VITE_API_URL termina en "/api" — el hub vive en la raíz del sitio, no bajo /api.
  const apiBaseUrl: string = import.meta.env.VITE_API_URL
  const siteBaseUrl = apiBaseUrl.endsWith('/api') ? apiBaseUrl.slice(0, -4) : apiBaseUrl
  const conn = new signalR.HubConnectionBuilder()
    .withUrl(`${siteBaseUrl}/hubs/updates`, { accessTokenFactory: () => auth.token ?? '' })
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build()

  conn.on('sessionRevoked', () => {
    auth.logout()
    window.location.href = '/login'
  })

  starting = conn
    .start()
    .then(() => {
      connection = conn
      return conn
    })
    .catch((err) => {
      console.error('No se pudo conectar el canal en tiempo real:', err)
      starting = null
      throw err
    })

  return starting
}

// Se suscribe a "algo cambió" para las entidades indicadas mientras el componente que llama esté montado
// (se desconecta solo al desmontarse). El handler recibe solo el id/acción: quien lo use vuelve a pedir el
// recurso por la API de siempre — nunca confía en datos que vengan por el canal.
export function useRealtimeUpdates(entities: string[], handler: (event: EntityChangedEvent) => void) {
  const wrapped = (event: EntityChangedEvent) => {
    if (entities.includes(event.entity)) handler(event)
  }

  getConnection()
    .then((conn) => conn.on('entityChanged', wrapped))
    .catch(() => {
      /* ya se registró arriba; la vista sigue funcionando sin tiempo real */
    })

  onScopeDispose(() => {
    connection?.off('entityChanged', wrapped)
  })
}
