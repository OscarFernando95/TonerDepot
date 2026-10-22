<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as ordersApi from '../../api/maintenanceOrders'
import { MaintenanceOrderStatusLabels, type MaintenanceOrderDto } from '../../api/types'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { usePagedList } from '../../composables/usePagedList'
import { useStatusTag } from '../../composables/useStatusTag'
import {
  formatPlace,
  formatScheduledDate,
  maintenanceIncludesLabels,
  maintenanceStatusLabel
} from '../../composables/useMaintenanceOrderInfo'

// Vista de solo lectura: el Cliente ve los mantenimientos de SU empresa (el backend lo acota), sin acciones.
const PAGE_SIZE = 25
const filters = reactive({ status: '', search: '' })
const {
  items: orders,
  total,
  page,
  loading,
  reload: loadOrders
} = usePagedList<MaintenanceOrderDto, typeof filters>((params) => ordersApi.listMaintenanceOrdersPaged(params).then((r) => r.data), {
  filters,
  pageSize: PAGE_SIZE,
  label: 'MyMaintenanceView',
  onError: (err: any) => ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los mantenimientos.')
})

// La búsqueda por texto espera a que se deje de escribir: sin esto cada tecla sería una petición.
const searchText = ref('')
let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(searchText, (value) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => (filters.search = value.trim()), 350)
})
onBeforeUnmount(() => clearTimeout(searchTimer))

const statusOptions = Object.entries(MaintenanceOrderStatusLabels).map(([value, label]) => ({ value, label }))
const hasFilters = computed(() => !!(filters.status || filters.search))

function clearFilters() {
  searchText.value = ''
  Object.assign(filters, { status: '', search: '' })
}

const { tagType: statusTagType } = useStatusTag('maintenanceOrder')

// Se actualiza sola cuando el equipo técnico programa, asigna o completa un mantenimiento (o tras un corte de red).
useRealtimeUpdates(['MaintenanceOrder'], () => loadOrders())
</script>

<template>
  <div>
    <h1>Mantenimientos</h1>
    <p class="hint">Mantenimientos programados de tus equipos y su avance.</p>

    <div class="filters-bar">
      <el-input v-model="searchText" clearable placeholder="Buscar por serie, modelo o marca" style="width: 300px" />
      <el-select v-model="filters.status" clearable placeholder="Estado" style="width: 170px">
        <el-option v-for="o in statusOptions" :key="o.value" :label="o.label" :value="o.value" />
      </el-select>
      <el-button v-if="hasFilters" link @click="clearFilters">Limpiar filtros</el-button>
      <span class="results-count">{{ total }} {{ total === 1 ? 'resultado' : 'resultados' }}</span>
    </div>

    <el-table :data="orders" v-loading="loading" stripe>
      <el-table-column label="Equipo" min-width="200">
        <template #default="{ row }">
          <div class="cell-stack">
            <span class="cell-main">{{ row.assetBrandName }} {{ row.assetModel }}</span>
            <span class="cell-sub">Serie {{ row.assetSerialNumber }}</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column label="Sede" min-width="180">
        <template #default="{ row }">{{ formatPlace(row.clientLocationName, row.cityName) }}</template>
      </el-table-column>
      <el-table-column label="Estado" width="130">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">{{ maintenanceStatusLabel(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Programado para" width="150">
        <template #default="{ row }">{{ formatScheduledDate(row.scheduledDate) }}</template>
      </el-table-column>
      <el-table-column label="Técnico" width="160">
        <template #default="{ row }">{{ row.technicianName ?? 'Por asignar' }}</template>
      </el-table-column>
      <el-table-column label="Incluye" min-width="200">
        <template #default="{ row }">
          <span class="includes">
            <el-tag v-for="label in maintenanceIncludesLabels(row)" :key="label" size="small" type="info">{{ label }}</el-tag>
          </span>
        </template>
      </el-table-column>
      <template #empty>
        <p class="empty">
          <template v-if="hasFilters">Ningún mantenimiento coincide con los filtros.</template>
          <template v-else>
            Todavía no hay mantenimientos programados. Cuando el equipo técnico agende una visita a uno de tus equipos,
            la verás aquí.
          </template>
        </p>
      </template>
    </el-table>

    <el-pagination
      v-if="total > PAGE_SIZE"
      v-model:current-page="page"
      class="pager"
      layout="prev, pager, next"
      :page-size="PAGE_SIZE"
      :total="total"
      background
    />
  </div>
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.filters-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.results-count {
  margin-left: auto;
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
}

.cell-stack {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.cell-main {
  color: var(--flap-ink);
  font-weight: 600;
}

.cell-sub {
  color: var(--el-text-color-secondary);
  font-size: var(--text-sm);
}

.includes {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}

.empty {
  margin: 0;
  padding: 1rem 0;
  color: var(--el-text-color-secondary);
}

.pager {
  margin-top: 1rem;
  justify-content: flex-end;
}
</style>
