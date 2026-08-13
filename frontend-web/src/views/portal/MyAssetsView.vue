<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as assetsApi from '../../api/assets'
import { AssetLifecycleStatusLabels, type AssetDto } from '../../api/types'

const assets = ref<AssetDto[]>([])
const loading = ref(false)

function statusTagType(status: string) {
  switch (status) {
    case 'Instalado':
      return 'success'
    case 'EnMantenimiento':
      return 'warning'
    case 'DadoDeBaja':
      return 'danger'
    default:
      return 'info'
  }
}

async function loadAssets() {
  loading.value = true
  try {
    const { data } = await assetsApi.listAssets()
    assets.value = data
  } finally {
    loading.value = false
  }
}

onMounted(loadAssets)
</script>

<template>
  <div>
    <h1>Mis activos</h1>
    <p class="hint">Impresoras y equipos instalados actualmente en tus sedes.</p>

    <el-table :data="assets" v-loading="loading" stripe>
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

    <p v-if="!loading && assets.length === 0" class="muted">
      Todavía no tienes activos instalados. Cuando el equipo técnico instale una impresora en una de tus sedes,
      aparecerá aquí.
    </p>
  </div>
</template>

<style scoped>
.hint {
  color: #6b7280;
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
  margin-top: 1rem;
}
</style>
