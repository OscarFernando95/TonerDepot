<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import * as evidenceApi from '../api/evidence'
import { useRealtimeUpdates } from '../composables/useRealtime'

// Fotos de evidencia (antes / después) de un ticket u orden, solo para el staff.
const props = defineProps<{ ticketId?: string; orderId?: string }>()

interface Photo {
  id: string
  kind: 'Antes' | 'Despues'
  uploadedAt: string
  url: string
}

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
onBeforeUnmount(() => photos.value.forEach((p) => URL.revokeObjectURL(p.url)))
</script>

<template>
  <div v-loading="loading">
    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="!loading && photos.length === 0" class="muted">Todavía no hay fotos de evidencia.</p>
    <div class="grid">
      <figure v-for="p in photos" :key="p.id" class="photo">
        <el-image :src="p.url" :preview-src-list="photos.map((x) => x.url)" fit="cover" class="thumb" preview-teleported />
        <figcaption>
          <el-tag size="small" :type="p.kind === 'Antes' ? 'warning' : 'success'">{{ p.kind === 'Antes' ? 'Antes' : 'Después' }}</el-tag>
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
