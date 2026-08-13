<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as contractsApi from '../../api/contracts'
import * as clientsApi from '../../api/clients'
import type { ClientDto, ContractDto } from '../../api/types'

const router = useRouter()

const contracts = ref<ContractDto[]>([])
const clients = ref<ClientDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const form = reactive({
  clientId: '',
  startDate: '',
  endDate: '',
  includedPrintsPerMonth: undefined as number | undefined,
  pricePerExtraPage: undefined as number | undefined,
  notes: ''
})

async function loadData() {
  loading.value = true
  try {
    const [contractsRes, clientsRes] = await Promise.all([contractsApi.listContracts(), clientsApi.listClients()])
    contracts.value = contractsRes.data
    clients.value = clientsRes.data
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
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Contratos</h1>
      <el-button type="primary" @click="openCreateDialog" :disabled="clients.length === 0">Nuevo contrato</el-button>
    </div>

    <el-table :data="contracts" v-loading="loading" stripe @row-click="goToDetail" class="clickable-rows">
      <el-table-column prop="clientName" label="Cliente" />
      <el-table-column label="Vigencia" width="220">
        <template #default="{ row }">
          {{ new Date(row.startDate).toLocaleDateString() }} —
          {{ row.endDate ? new Date(row.endDate).toLocaleDateString() : 'indefinida' }}
        </template>
      </el-table-column>
      <el-table-column label="Estado" width="120">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Impresiones incluidas" width="160">
        <template #default="{ row }">{{ row.includedPrintsPerMonth ?? '—' }}</template>
      </el-table-column>
      <el-table-column label="Activos" width="90">
        <template #default="{ row }">{{ row.assetCount }}</template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nuevo contrato" width="480px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Cliente">
          <el-select v-model="form.clientId" style="width: 100%" filterable placeholder="Selecciona un cliente">
            <el-option v-for="c in clients" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
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
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
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
