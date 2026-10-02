<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import * as contractsApi from '../../api/contracts'
import * as clientsApi from '../../api/clients'
import CreateClientDialog from '../../components/CreateClientDialog.vue'
import type { ClientDto, ContractDto } from '../../api/types'
import { formatDateUTC } from '../../utils/date'

const router = useRouter()

const contracts = ref<ContractDto[]>([])
const clients = ref<ClientDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)
const createClientDialogRef = ref<InstanceType<typeof CreateClientDialog> | null>(null)

const filters = reactive({ cityName: '', clientId: '' })

function matches(c: ContractDto, exclude: 'cityName' | 'clientId') {
  const cityOk = exclude === 'cityName' || !filters.cityName || c.cityNames.includes(filters.cityName)
  const clientOk = exclude === 'clientId' || !filters.clientId || c.clientId === filters.clientId
  return cityOk && clientOk
}

const cityOptions = computed(() =>
  [...new Set(contracts.value.filter((c) => matches(c, 'cityName')).flatMap((c) => c.cityNames))].sort()
)
const clientOptions = computed(() => {
  const seen = new Map<string, string>()
  for (const c of contracts.value.filter((x) => matches(x, 'clientId'))) {
    seen.set(c.clientId, c.clientName)
  }
  return [...seen.entries()].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name))
})

const filteredContracts = computed(() => contracts.value.filter((c) => matches(c, 'cityName') && matches(c, 'clientId')))

const emptyContractsText = computed(() =>
  filters.cityName || filters.clientId ? 'No hay contratos que coincidan con los filtros.' : 'No hay contratos registrados.'
)

const form = reactive({
  clientId: '',
  startDate: '',
  endDate: '',
  includedPrintsPerMonth: undefined as number | undefined,
  pricePerExtraPage: undefined as number | undefined,
  notes: ''
})

async function loadData(silent = false) {
  if (!silent) loading.value = true
  try {
    const [contractsRes, clientsRes] = await Promise.all([contractsApi.listContracts(), clientsApi.listClients()])
    contracts.value = contractsRes.data
    clients.value = clientsRes.data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los contratos.')
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.clientId = ''
  form.startDate = ''
  form.endDate = ''
  form.includedPrintsPerMonth = undefined
  form.pricePerExtraPage = undefined
  form.notes = ''
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await contractsApi.createContract({
      clientId: form.clientId,
      startDate: new Date(form.startDate).toISOString(),
      endDate: form.endDate ? new Date(form.endDate).toISOString() : null,
      includedPrintsPerMonth: form.includedPrintsPerMonth ?? null,
      pricePerExtraPage: form.pricePerExtraPage ?? null,
      notes: form.notes || null
    })
    ElMessage.success('Contrato creado.')
    dialogVisible.value = false
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el contrato.')
  } finally {
    saving.value = false
  }
}

function onClientCreatedFromContract(newClient: ClientDto) {
  clients.value.push(newClient)
  form.clientId = newClient.id
}

function statusTagType(status: string) {
  switch (status) {
    case 'Activo':
      return 'success'
    case 'Vencido':
      return 'warning'
    case 'Cancelado':
      return 'danger'
    default:
      return 'info'
  }
}

function goToDetail(contract: ContractDto) {
  router.push({ name: 'contract-detail', params: { id: contract.id } })
}

onMounted(loadData)
useRealtimeUpdates(['Contract'], () => loadData(true))
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Contratos</h1>
      <el-button type="primary" @click="openCreateDialog">Nuevo contrato</el-button>
    </div>

    <div class="filters-bar">
      <el-select v-model="filters.cityName" clearable filterable placeholder="Filtrar por ciudad" style="width: 220px">
        <el-option v-for="c in cityOptions" :key="c" :label="c" :value="c" />
      </el-select>
      <el-select v-model="filters.clientId" clearable filterable placeholder="Filtrar por cliente" style="width: 240px">
        <el-option v-for="c in clientOptions" :key="c.id" :label="c.name" :value="c.id" />
      </el-select>
    </div>

    <el-table
      :data="filteredContracts"
      v-loading="loading"
      stripe
      @row-click="goToDetail"
      class="clickable-rows"
      :empty-text="emptyContractsText"
    >
      <el-table-column prop="clientName" label="Cliente" sortable />
      <el-table-column prop="startDate" label="Vigencia" width="220" sortable>
        <template #default="{ row }">
          {{ formatDateUTC(row.startDate) }} —
          {{ row.endDate ? formatDateUTC(row.endDate) : 'indefinida' }}
        </template>
      </el-table-column>
      <el-table-column prop="status" label="Estado" width="120" sortable>
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="includedPrintsPerMonth" label="Impresiones incluidas" width="160" sortable>
        <template #default="{ row }">{{ row.includedPrintsPerMonth ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="assetCount" label="Activos" width="90" sortable>
        <template #default="{ row }">{{ row.assetCount }}</template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo contrato" width="480px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Cliente">
          <div class="client-select-row">
            <el-select v-model="form.clientId" style="flex: 1" filterable placeholder="Selecciona un cliente">
              <el-option v-for="c in clients" :key="c.id" :label="c.name" :value="c.id" />
            </el-select>
            <el-button :icon="Plus" circle title="Crear cliente" @click="createClientDialogRef?.open()" />
          </div>
        </el-form-item>
        <div class="form-grid">
          <el-form-item label="Fecha inicio">
            <el-date-picker v-model="form.startDate" type="date" style="width: 100%" value-format="YYYY-MM-DD" />
          </el-form-item>
          <el-form-item label="Fecha fin (opcional)">
            <el-date-picker v-model="form.endDate" type="date" style="width: 100%" value-format="YYYY-MM-DD" />
          </el-form-item>
        </div>
        <div class="form-grid">
          <el-form-item label="Impresiones incluidas / mes">
            <el-input-number v-model="form.includedPrintsPerMonth" :min="0" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Precio por página extra">
            <el-input-number v-model="form.pricePerExtraPage" :min="0" :precision="2" style="width: 100%" />
          </el-form-item>
        </div>
        <el-form-item label="Notas">
          <el-input v-model="form.notes" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">Guardar</el-button>
      </template>
    </el-dialog>

    <CreateClientDialog ref="createClientDialogRef" @created="onClientCreatedFromContract" />
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.client-select-row {
  display: flex;
  gap: 0.5rem;
  width: 100%;
}

.filters-bar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 1rem;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
