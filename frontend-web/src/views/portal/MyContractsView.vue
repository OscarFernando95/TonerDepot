<script setup lang="ts">
import { onMounted, ref } from 'vue'
import * as contractsApi from '../../api/contracts'
import type { ContractDto } from '../../api/types'

const contracts = ref<ContractDto[]>([])
const loading = ref(false)

function statusTagType(status: string) {
  switch (status) {
    case 'Activo':
      return 'success'
    case 'Vencido':
      return 'warning'
    case 'Cancelado':
      return 'info'
    default:
      return 'primary'
  }
}

async function loadContracts() {
  loading.value = true
  try {
    const { data } = await contractsApi.listContracts()
    contracts.value = data
  } finally {
    loading.value = false
  }
}

onMounted(loadContracts)
</script>

<template>
  <div>
    <h1>Mis contratos</h1>
    <p class="hint">Condiciones y vigencia de tus contratos de alquiler.</p>

    <el-table :data="contracts" v-loading="loading" stripe>
      <el-table-column label="Vigencia" width="220">
        <template #default="{ row }">
          {{ new Date(row.startDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) }} —
          {{ row.endDate ? new Date(row.endDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) : 'indefinida' }}
        </template>
      </el-table-column>
      <el-table-column label="Estado" width="120">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Impresiones incluidas / mes" width="180">
        <template #default="{ row }">{{ row.includedPrintsPerMonth ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Precio página extra" width="160">
        <template #default="{ row }">{{ row.pricePerExtraPage != null ? `$${row.pricePerExtraPage}` : '—' }}</template>
      </el-table-column>
      <el-table-column label="Activos" width="90">
        <template #default="{ row }">{{ row.assetCount }}</template>
      </el-table-column>
      <el-table-column prop="notes" label="Notas" />
    </el-table>

    <p v-if="!loading && contracts.length === 0" class="muted">Todavía no tienes contratos registrados.</p>
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
</style>
