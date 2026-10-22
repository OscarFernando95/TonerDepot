<script setup lang="ts">
import { computed, nextTick, ref, toRef, watch } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import {
  COMMENT_MAX_LENGTH,
  authorRoleLabel,
  formatCommentDate,
  formatCommentDateAbsolute,
  isThreadClosed,
  threadEventText,
  useMaintenanceThread
} from '../../composables/useMaintenanceThread'

// Hilo compartido de una orden de mantenimiento (cliente, técnico y staff). Las entradas 'Programacion' se pintan como
// un evento; los comentarios llevan autor, rol y fecha, y los del cliente van resaltados. Si la orden está
// Completada o Cancelada el hilo es solo lectura. Se recarga solo cuando el servidor avisa de un cambio en la orden.
const props = withDefaults(
  defineProps<{
    orderId: string
    orderStatus: string
    // Texto de ayuda sobre la caja de escritura (p. ej. para el cliente).
    helpText?: string
    // Alto máximo de la lista antes de hacer scroll.
    maxHeight?: string
    // Fuerza solo lectura aunque la orden siga abierta (p. ej. el técnico con la visita ya en curso).
    readOnly?: boolean
  }>(),
  { helpText: undefined, maxHeight: '320px', readOnly: false }
)

const emit = defineEmits<{ sent: [] }>()

const { entries, loading, sending, loadFailed, sendError, draft, validation, canSend, load, send, reset } =
  useMaintenanceThread(toRef(props, 'orderId'))

const closed = computed(() => isThreadClosed(props.orderStatus))
const locked = computed(() => closed.value || props.readOnly)
// La fecha relativa se calcula una vez por carga de entradas, no por render de cada fila.
const rows = computed(() => {
  const now = new Date()
  return entries.value.map((entry) => ({
    entry,
    isEvent: entry.kind === 'Programacion',
    eventText: threadEventText(entry, now),
    roleLabel: authorRoleLabel(entry.authorRole),
    isClient: entry.authorRole === 'Cliente',
    when: formatCommentDate(entry.createdAt, now),
    whenFull: formatCommentDateAbsolute(entry.createdAt, now)
  }))
})

const listEl = ref<HTMLElement | null>(null)
async function scrollToEnd() {
  await nextTick()
  if (listEl.value) listEl.value.scrollTop = listEl.value.scrollHeight
}

watch(
  () => props.orderId,
  () => {
    reset()
    void load().then(scrollToEnd)
  },
  { immediate: true }
)
watch(
  () => entries.value.length,
  () => void scrollToEnd()
)

async function submit() {
  if (await send()) emit('sent')
}

// Un comentario nuevo (o la orden que cambia) llega por tiempo real; resync tras un corte de red. Quien aloja el
// hilo se actualiza por su propia suscripción, así que aquí solo se recarga el hilo.
useRealtimeUpdates(['MaintenanceOrder'], (event) => {
  if (event.action === 'resync' || event.id === props.orderId) {
    void load(true)
  }
})
</script>

<template>
  <div class="thread">
    <div ref="listEl" v-loading="loading" class="thread-list" :style="{ maxHeight }" aria-live="polite">
      <p v-if="loadFailed" class="thread-empty">No se pudo cargar la conversación. Intenta de nuevo en unos segundos.</p>
      <p v-else-if="!loading && rows.length === 0" class="thread-empty">Aún no hay mensajes.</p>
      <template v-for="row in rows" :key="row.entry.id">
        <div v-if="row.isEvent" class="thread-event" :title="row.whenFull">
          <span>{{ row.eventText }}</span>
          <span class="thread-when">{{ row.when }}</span>
        </div>
        <div v-else class="thread-comment" :class="{ 'is-client': row.isClient }">
          <div class="thread-meta">
            <span class="thread-author">{{ row.entry.authorName }}</span>
            <span class="thread-role">{{ row.roleLabel }}</span>
            <span class="thread-when" :title="row.whenFull">{{ row.when }}</span>
          </div>
          <p class="thread-body">{{ row.entry.body }}</p>
        </div>
      </template>
    </div>

    <p v-if="locked" class="thread-closed">
      {{ closed ? 'Esta orden ya está cerrada: la conversación es solo de lectura.' : 'La conversación es solo de lectura mientras la visita está en curso.' }}
    </p>
    <div v-else class="thread-compose">
      <p v-if="helpText" class="thread-help">{{ helpText }}</p>
      <el-input
        v-model="draft"
        type="textarea"
        :rows="3"
        resize="none"
        :maxlength="COMMENT_MAX_LENGTH"
        show-word-limit
        placeholder="Escribe un mensaje"
        :disabled="sending"
        @keydown.ctrl.enter.prevent="submit"
        @keydown.meta.enter.prevent="submit"
      />
      <p v-if="validation.error || sendError" class="thread-error" role="alert">{{ validation.error ?? sendError }}</p>
      <div class="thread-actions">
        <el-button type="primary" class="thread-send" :loading="sending" :disabled="!canSend" @click="submit">Enviar</el-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.thread-list {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  min-height: 80px;
  overflow-y: auto;
  padding: 0.25rem 0.1rem;
}

.thread-empty {
  margin: 0;
  padding: 1rem 0;
  color: var(--el-text-color-secondary);
  font-size: var(--text-md);
}

.thread-event {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.25rem 0.75rem;
  padding: 0.45rem 0.7rem;
  border-left: 3px solid var(--signal-amber);
  background: var(--signal-amber-wash);
  color: var(--flap-ink);
  font-size: var(--text-md);
}

.thread-comment {
  padding: 0.5rem 0.7rem;
  border-left: 3px solid var(--board-seam);
  background: var(--board-panel-raised);
}

/* Lo que escribe el cliente se destaca para que el equipo no lo pase por alto. */
.thread-comment.is-client {
  border-left-color: var(--signal-blue-bright);
  background: var(--signal-blue-wash);
}

.thread-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.15rem 0.6rem;
  margin-bottom: 0.2rem;
}

.thread-author {
  color: var(--flap-ink);
  font-weight: 600;
  font-size: var(--text-md);
}

.thread-role {
  color: var(--flap-ink-dim);
  font-size: var(--text-2xs);
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.thread-when {
  margin-left: auto;
  color: var(--flap-ink-dim);
  font-size: var(--text-xs);
  white-space: nowrap;
}

.thread-body {
  margin: 0;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  font-size: var(--text-md);
}

.thread-closed {
  margin: 0.75rem 0 0;
  color: var(--el-text-color-secondary);
  font-size: var(--text-md);
}

.thread-compose {
  margin-top: 0.75rem;
}

.thread-help {
  margin: 0 0 0.4rem;
  color: var(--el-text-color-secondary);
  font-size: var(--text-md);
}

.thread-error {
  margin: 0.35rem 0 0;
  color: var(--signal-red);
  font-size: var(--text-md);
}

.thread-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 0.5rem;
}

.thread-send {
  min-height: 44px;
  padding: 0 1.4rem;
}
</style>
