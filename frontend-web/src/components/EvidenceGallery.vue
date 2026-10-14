<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import * as evidenceApi from '../api/evidence'
import { useRealtimeUpdates } from '../composables/useRealtime'

// Fotos de evidencia (antes / después) de un ticket u orden, solo para el staff.
const props = defineProps<{ ticketId?: string; orderId?: string }>()

interface Photo {
  id: string
  kind: 'Antes' | 'Despues' | 'Contador'
  uploadedAt: string
  // null = archivo borrado por la política de retención (AUDIT.md H-44): se muestra un recuadro en su lugar.
  url: string | null
}

const KIND_TAG = {
  Antes: { label: 'Antes', type: 'warning' },
  Despues: { label: 'Después', type: 'success' },
  Contador: { label: 'Contador', type: 'info' }
} as const

const photos = ref<Photo[]>([])
const loading = ref(false)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const { data } = await evidenceApi.listEvidence({ ticketId: props.ticketId, orderId: props.orderId })
    photos.value = await Promise.all(
      data.map(async (e) => {
        // Una foto purgada no se pide: daría 404 y, con Promise.all, tumbaría la galería entera.
        if (e.contentPurgedAt) {
          return { id: e.id, kind: e.kind, uploadedAt: e.uploadedAt, url: null }
        }
        const blob = (await evidenceApi.getEvidenceContent(e.id)).data
        return { id: e.id, kind: e.kind, uploadedAt: e.uploadedAt, url: URL.createObjectURL(blob) }
      })
    )
  } catch (err: any) {
    console.error('No se pudo cargar la evidencia:', err)
    error.value = err.response?.data?.title ?? 'No se pudo cargar la evidencia.'
  } finally {
    loading.value = false
  }
}

onMounted(load)

// Una foto nueva del técnico (Evidence) aparece sola, sin recargar la página. No filtramos por ticket/orden
// porque el aviso no lleva ese dato (solo id de la evidencia); recargar la galería visible es barato.
useRealtimeUpdates(['Evidence'], () => load())
// Solo las fotos con archivo entran al visor ampliado.
const previewUrls = computed(() => photos.value.flatMap((p) => (p.url ? [p.url] : [])))

onBeforeUnmount(() => photos.value.forEach((p) => p.url && URL.revokeObjectURL(p.url)))
</script>

<template>
  <div v-loading="loading">
    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="!loading && photos.length === 0" class="muted">Todavía no hay fotos de evidencia.</p>
    <div class="grid">
      <figure v-for="p in photos" :key="p.id" class="photo">
        <el-image v-if="p.url" :src="p.url" :preview-src-list="previewUrls" fit="cover" class="thumb" preview-teleported />
        <div v-else class="thumb purged">Foto eliminada por la política de retención</div>
        <figcaption>
          <el-tag size="small" :type="KIND_TAG[p.kind].type">{{ KIND_TAG[p.kind].label }}</el-tag>
          <span class="when">{{ new Date(p.uploadedAt).toLocaleString() }}</span>
        </figcaption>
      </figure>
    </div>
  </div>
</template>

<style scoped>
.grid {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
}

.photo {
  margin: 0;
}

.thumb {
  width: 180px;
  height: 135px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
}

.purged {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0.75rem;
  box-sizing: border-box;
  text-align: center;
  font-size: 0.8rem;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
}

figcaption {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.35rem;
}

.when,
.muted {
  color: var(--el-text-color-secondary);
  font-size: 0.8rem;
}

.error {
  color: var(--el-color-danger);
}
</style>
