// Hilo de comentarios de una orden de mantenimiento, compartido entre cliente, técnico y staff. Aquí vive la lógica
// pura (etiquetas, textos, validación, "comentarios nuevos") y el composable con el estado de carga y envío; el
// componente MaintenanceOrderThread solo lo pinta.
import { computed, reactive, ref, type Ref } from 'vue'
import * as ordersApi from '../api/maintenanceOrders'
import type { MaintenanceCommentAuthorRole, MaintenanceOrderCommentDto } from '../api/types'
import { apiErrorMessage } from '../utils/apiError'
import { formatPlannedAt } from './useMaintenanceSchedule'

export const COMMENT_MAX_LENGTH = 1000

const ROLE_LABELS: Record<MaintenanceCommentAuthorRole, string> = {
  Cliente: 'Cliente',
  Tecnico: 'Técnico',
  Administrador: 'Administrador',
  Coordinador: 'Coordinador'
}

// Etiqueta legible del rol del autor. hasOwnProperty: un rol "constructor" no debe devolver la función heredada.
export function authorRoleLabel(role: string): string {
  return Object.prototype.hasOwnProperty.call(ROLE_LABELS, role) ? ROLE_LABELS[role as MaintenanceCommentAuthorRole] : role
}

// Hilo cerrado (solo lectura): el servidor responde 409 al comentar en una orden Completada o Cancelada.
export function isThreadClosed(status: string): boolean {
  return status === 'Completada' || status === 'Cancelada'
}

export interface CommentValidation {
  valid: boolean
  // Largo del texto ya recortado, el que cuenta contra el límite y el que se envía.
  length: number
  error: string | null
}

export function validateCommentBody(text: string): CommentValidation {
  const length = text.trim().length
  if (length === 0) return { valid: false, length, error: null } // vacío: solo se deshabilita el envío, sin regañar
  if (length > COMMENT_MAX_LENGTH) {
    return { valid: false, length, error: `El comentario no puede pasar de ${COMMENT_MAX_LENGTH} caracteres.` }
  }
  return { valid: true, length, error: null }
}

// Mismos formatos que la fecha programada: instanciar Intl es caro, aquí se hace una vez por módulo.
const absoluteFormatter = new Intl.DateTimeFormat('es-CO', {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23'
})
const absoluteFormatterWithYear = new Intl.DateTimeFormat('es-CO', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23'
})

// Fecha absoluta completa ("30 oct, 10:00"), para el tooltip y para entradas viejas.
export function formatCommentDateAbsolute(value: string, now: Date = new Date()): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return (date.getFullYear() === now.getFullYear() ? absoluteFormatter : absoluteFormatterWithYear).format(date)
}

// Reciente -> relativa ("hace 5 min", "hace 3 h"); de ayer en adelante -> absoluta.
export function formatCommentDate(value: string, now: Date = new Date()): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  const minutes = Math.floor((now.getTime() - date.getTime()) / 60000)
  if (minutes < 1) return 'justo ahora' // incluye relojes con unos segundos de desfase (fecha "en el futuro")
  if (minutes < 60) return `hace ${minutes} min`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `hace ${hours} h`
  return formatCommentDateAbsolute(value, now)
}

// Texto del evento "el técnico programó": "Juan programó el mantenimiento para jue, 30 oct, 10:00".
export function threadEventText(entry: Pick<MaintenanceOrderCommentDto, 'authorName' | 'plannedAt' | 'body'>, now: Date = new Date()): string {
  if (!entry.plannedAt) return entry.body
  return `${entry.authorName} programó el mantenimiento para el ${formatPlannedAt(entry.plannedAt, now)}`
}

// ¿Hay algo en el hilo posterior a lo último que esta persona vio?
export function hasUnseenComments(lastCommentAt: string | null | undefined, seenAt: string | null | undefined): boolean {
  if (!lastCommentAt) return false
  const last = new Date(lastCommentAt).getTime()
  if (Number.isNaN(last)) return false
  if (!seenAt) return true
  const seen = new Date(seenAt).getTime()
  return Number.isNaN(seen) || last > seen
}

// Último hilo visto por orden, en este navegador. Es una comodidad por persona: si el almacenamiento falla (ventana
// privada, bloqueado) simplemente todo aparece como "nuevo" hasta abrirlo. Reactivo para que las listas se actualicen.
const SEEN_PREFIX = 'toner.mantenimiento.hilo-visto.'
const seenState = reactive<Record<string, string>>({})

export function getSeenAt(orderId: string): string | null {
  if (seenState[orderId]) return seenState[orderId]
  try {
    const stored = localStorage.getItem(SEEN_PREFIX + orderId)
    if (stored) {
      seenState[orderId] = stored
      return stored
    }
  } catch {
    // almacenamiento no disponible: se trata como no visto
  }
  return null
}

export function markThreadSeen(orderId: string, upTo: string | null | undefined) {
  if (!upTo) return
  const current = getSeenAt(orderId)
  if (current && new Date(current).getTime() >= new Date(upTo).getTime()) return
  seenState[orderId] = upTo
  try {
    localStorage.setItem(SEEN_PREFIX + orderId, upTo)
  } catch {
    // sin almacenamiento: queda solo en memoria
  }
}

export function orderHasNewComments(order: { id: string; lastCommentAt: string | null }): boolean {
  return hasUnseenComments(order.lastCommentAt, getSeenAt(order.id))
}

// Estado y acciones del hilo de una orden. `orderId` es un ref para que el componente pueda reutilizarse con otra orden.
export function useMaintenanceThread(orderId: Ref<string>) {
  const entries = ref<MaintenanceOrderCommentDto[]>([])
  const loading = ref(false)
  const sending = ref(false)
  const loadFailed = ref(false)
  const sendError = ref<string | null>(null)
  const draft = ref('')

  const validation = computed(() => validateCommentBody(draft.value))
  const canSend = computed(() => validation.value.valid && !sending.value)

  // `silent`: recarga por tiempo real, sin parpadeo de carga.
  async function load(silent = false) {
    const requested = orderId.value
    if (!silent) loading.value = true
    try {
      const { data } = await ordersApi.listOrderComments(requested)
      if (requested !== orderId.value) return // cambió de orden mientras cargaba: la respuesta ya no aplica
      entries.value = data
      loadFailed.value = false
      const last = data[data.length - 1]
      if (last) markThreadSeen(requested, last.createdAt)
    } catch (err) {
      console.error('MaintenanceOrderThread: no se pudo cargar el hilo', err)
      loadFailed.value = true
    } finally {
      loading.value = false
    }
  }

  // Devuelve true si se envió (para que el componente avise a quien lo usa).
  async function send(): Promise<boolean> {
    if (!canSend.value) return false
    sending.value = true
    sendError.value = null
    try {
      const { data } = await ordersApi.addOrderComment(orderId.value, draft.value.trim())
      draft.value = ''
      entries.value = [...entries.value, data]
      markThreadSeen(orderId.value, data.createdAt)
      return true
    } catch (err: any) {
      console.error('MaintenanceOrderThread: no se pudo enviar el comentario', err)
      sendError.value = apiErrorMessage(err, 'No se pudo enviar el comentario.')
      return false
    } finally {
      sending.value = false
    }
  }

  function reset() {
    entries.value = []
    draft.value = ''
    sendError.value = null
    loadFailed.value = false
  }

  return { entries, loading, sending, loadFailed, sendError, draft, validation, canSend, load, send, reset }
}
