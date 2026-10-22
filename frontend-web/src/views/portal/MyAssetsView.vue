<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { ElMessage } from 'element-plus'
import * as assetsApi from '../../api/assets'
import { AssetLifecycleStatuses, AssetLifecycleStatusLabels, type AssetDto } from '../../api/types'
import { useStatusTag } from '../../composables/useStatusTag'

const assets = ref<AssetDto[]>([])
const loading = ref(false)
const loaded = ref(false)

const { tagType: statusTagType } = useStatusTag('asset')

// Los equipos que un técnico aún no instala van aparte de los ya instalados: no son lo mismo que un cliente espera ver.
const pendingInstallations = computed(() =>
  assets.value.filter((a) => a.lifecycleStatus === AssetLifecycleStatuses.PendienteInstalacion)
)
const installedAssets = computed(() =>
  assets.value.filter((a) => a.lifecycleStatus !== AssetLifecycleStatuses.PendienteInstalacion)
)

async function loadAssets(silent = false) {
  if (!silent) loading.value = true
  try {
    // El backend limita a un Cliente a los activos de sus sedes; se recorren todas las páginas (AUDIT.md H-11).
    assets.value = await assetsApi.listAllAssets()
    loaded.value = true
  } catch (err: any) {
    console.error('MyAssetsView: no se pudieron cargar los activos', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar tus activos.')
  } finally {
    loading.value = false
  }
}

onMounted(loadAssets)
useRealtimeUpdates(['Asset'], () => loadAssets(true))
</script>

<template>
  <div>
    <h1>Mis activos</h1>
    <p class="hint">Impresoras y equipos instalados actualmente en tus sedes.</p>

    <section v-if="pendingInstallations.length > 0" class="pending" aria-labelledby="pending-title">
      <h2 id="pending-title" class="section-title">Instalaciones pendientes</h2>
      <p class="hint">Un técnico instalará estos equipos próximamente.</p>
      <ul class="pending-list">
        <li v-for="a in pendingInstallations" :key="a.id" class="pending-row">
          <div class="pending-main">
            <span class="pending-model">{{ a.assetBrandName }} {{ a.model }}</span>
            <span class="pending-meta">
              Serie {{ a.serialNumber }}
              <template v-if="a.currentClientLocationName"> · {{ a.currentClientLocationName }}</template>
              <template v-if="a.cityName"> · {{ a.cityName }}</template>
            </span>
          </div>
          <el-tag :type="statusTagType(a.lifecycleStatus)" size="small">Pendiente de instalar</el-tag>
        </li>
      </ul>
    </section>

    <h2 v-if="pendingInstallations.length > 0" class="section-title">Equipos instalados</h2>
    <el-table :data="installedAssets" v-loading="loading" stripe>
      <el-table-column label="Marca / Modelo">
        <template #default="{ row }">{{ row.assetBrandName }} {{ row.model }}</template>
      </el-table-column>
      <el-table-column prop="serialNumber" label="Número de serie" width="160" />
      <el-table-column prop="type" label="Tipo" width="130" />
      <el-table-column label="Sede" width="180">
        <template #default="{ row }">{{ row.currentClientLocationName ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Estado" width="150">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.lifecycleStatus)" size="small">
            {{ AssetLifecycleStatusLabels[row.lifecycleStatus as keyof typeof AssetLifecycleStatusLabels] }}
          </el-tag>
        </template>
      </el-table-column>
    </el-table>

    <p v-if="loaded && !loading && installedAssets.length === 0" class="muted">
      <template v-if="pendingInstallations.length > 0">
        Todavía no tienes equipos instalados. Cuando el técnico termine las instalaciones pendientes, aparecerán aquí.
      </template>
      <template v-else>
        Todavía no tienes activos instalados. Cuando el equipo técnico instale una impresora en una de tus sedes,
        aparecerá aquí.
      </template>
    </p>
  </div>
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
  margin-top: 1rem;
}

.section-title {
  font-size: var(--text-md);
  margin: 1.25rem 0 0.35rem;
}

.pending {
  margin-bottom: 1.5rem;
}

.pending-list {
  list-style: none;
  margin: 0;
  padding: 0;
  border: 1px solid var(--board-seam-soft);
  border-left: 3px solid var(--signal-amber);
  background: var(--signal-amber-wash);
}

.pending-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.7rem 1rem;
}

.pending-row + .pending-row {
  border-top: 1px solid var(--board-seam-soft);
}

.pending-main {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 0;
}

.pending-model {
  color: var(--flap-ink);
  font-weight: 600;
}

.pending-meta {
  color: var(--el-text-color-secondary);
  font-size: var(--text-sm);
}
</style>
