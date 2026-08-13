<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as contractsApi from '../../api/contracts'
import * as contractAssetsApi from '../../api/contractAssets'
import * as assetsApi from '../../api/assets'
import * as clientLocationsApi from '../../api/clientLocations'
import {
  ContractStatuses,
  type AssetDto,
  type ClientLocationDto,
  type ContractAssetDto,
  type ContractDto
} from '../../api/types'

const route = useRoute()
const router = useRouter()
const contractId = route.params.id as string

const contract = ref<ContractDto | null>(null)
const contractAssets = ref<ContractAssetDto[]>([])
const assets = ref<AssetDto[]>([])
const locations = ref<ClientLocationDto[]>([])
const loading = ref(false)

// Solo se puede vincular un activo que esté disponible en bodega (ver ContractAssetService.AddAsync) —
// se filtra acá para no ofrecer opciones que el backend va a rechazar.
const availableAssets = computed(() => assets.value.filter((a) => a.lifecycleStatus === 'EnBodega'))

const statusOptions = Object.values(ContractStatuses)

const form = reactive({
  startDate: '',
  endDate: '',
  includedPrintsPerMonth: undefined as number | undefined,
  pricePerExtraPage: undefined as number | undefined,
  notes: ''
})
const saving = ref(false)
const changingStatus = ref(false)

const addAssetDialogVisible = ref(false)
const savingAsset = ref(false)
const addAssetForm = reactive({ assetId: '', clientLocationId: '' })

async function loadAll() {
  loading.value = true
  try {
    const [contractRes, contractAssetsRes, assetsRes] = await Promise.all([
      contractsApi.getContract(contractId),
      contractAssetsApi.listContractAssets(contractId),
      assetsApi.listAssets()
    ])
    contract.value = contractRes.data
    contractAssets.value = contractAssetsRes.data
    assets.value = assetsRes.data
    syncForm()

    // Acotado al cliente del contrato: el backend exige que la sede pertenezca a ese cliente.
    const { data: locationsData } = await clientLocationsApi.listClientLocations(contract.value.clientId)
    locations.value = locationsData
  } finally {
    loading.value = false
  }
}

function syncForm() {
  if (!contract.value) return
  form.startDate = contract.value.startDate.slice(0, 10)
  form.endDate = contract.value.endDate ? contract.value.endDate.slice(0, 10) : ''
  form.includedPrintsPerMonth = contract.value.includedPrintsPerMonth ?? undefined
  form.pricePerExtraPage = contract.value.pricePerExtraPage ?? undefined
  form.notes = contract.value.notes ?? ''
}

async function saveContract() {
  saving.value = true
  try {
    const { data } = await contractsApi.updateContract(contractId, {
      startDate: new Date(form.startDate).toISOString(),
      endDate: form.endDate ? new Date(form.endDate).toISOString() : null,
      includedPrintsPerMonth: form.includedPrintsPerMonth ?? null,
      pricePerExtraPage: form.pricePerExtraPage ?? null,
      notes: form.notes || null
    })
    contract.value = data
    ElMessage.success('Contrato actualizado.')
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo actualizar el contrato.')
  } finally {
    saving.value = false
  }
}

async function changeStatus(status: string) {
  if (!contract.value || status === contract.value.status) return
  changingStatus.value = true
  try {
    const { data } = await contractsApi.setContractStatus(contractId, status)
    contract.value = data
    ElMessage.success('Estado actualizado.')
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cambiar el estado.')
  } finally {
    changingStatus.value = false
  }
}

function openAddAssetDialog() {
  addAssetForm.assetId = ''
  addAssetForm.clientLocationId = ''
  addAssetDialogVisible.value = true
}

async function saveAddAsset() {
  savingAsset.value = true
  try {
    await contractAssetsApi.addContractAsset(contractId, {
      assetId: addAssetForm.assetId,
      clientLocationId: addAssetForm.clientLocationId
    })
    ElMessage.success('Activo vinculado al contrato — queda "Pendiente de instalar" en la sede elegida.')
    addAssetDialogVisible.value = false
    const [{ data: caData }, { data: contractData }, { data: assetsData }] = await Promise.all([
      contractAssetsApi.listContractAssets(contractId),
      contractsApi.getContract(contractId),
      assetsApi.listAssets()
    ])
    contractAssets.value = caData
    contract.value = contractData
    assets.value = assetsData
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo vincular el activo.')
  } finally {
    savingAsset.value = false
  }
}

async function endLink(contractAsset: ContractAssetDto) {
  await ElMessageBox.confirm(
    `¿Finalizar la vinculación de ${contractAsset.assetBrandName} ${contractAsset.assetModel} (${contractAsset.assetSerialNumber}) con este contrato?`,
    'Confirmar',
    { type: 'warning' }
  )
  await contractAssetsApi.endContractAsset(contractId, contractAsset.id)
  ElMessage.success('Vinculación finalizada.')
  const [{ data: caData }, { data: contractData }] = await Promise.all([
    contractAssetsApi.listContractAssets(contractId),
    contractsApi.getContract(contractId)
  ])
  contractAssets.value = caData
  contract.value = contractData
}

function goToClient() {
  if (contract.value) {
    router.push({ name: 'client-detail', params: { id: contract.value.clientId } })
  }
}

onMounted(loadAll)
</script>

<template>
  <div v-loading="loading">
    <template v-if="contract">
      <div class="page-header">
        <h1>
          Contrato —
          <el-link type="primary" @click="goToClient">{{ contract.clientName }}</el-link>
        </h1>
        <el-select
          :model-value="contract.status"
          style="width: 160px"
          :disabled="changingStatus"
          @change="changeStatus"
        >
          <el-option v-for="s in statusOptions" :key="s" :label="s" :value="s" />
        </el-select>
      </div>

      <el-card class="section-card">
        <template #header>Condiciones</template>
        <el-form :model="form" label-position="top">
          <div class="form-grid">
            <el-form-item label="Fecha inicio">
              <el-date-picker v-model="form.startDate" type="date" style="width: 100%" value-format="YYYY-MM-DD" />
            </el-form-item>
            <el-form-item label="Fecha fin (opcional)">
              <el-date-picker v-model="form.endDate" type="date" style="width: 100%" value-format="YYYY-MM-DD" />
            </el-form-item>
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
        <div class="section-actions">
          <el-button type="primary" :loading="saving" @click="saveContract">Guardar cambios</el-button>
        </div>
      </el-card>

      <el-card class="section-card">
        <template #header>
          <div class="section-card-header">
            <span>Activos vinculados</span>
            <el-button type="primary" size="small" @click="openAddAssetDialog">Vincular activo</el-button>
          </div>
        </template>

        <el-table :data="contractAssets" stripe>
          <el-table-column label="Activo">
            <template #default="{ row }">{{ row.assetBrandName }} {{ row.assetModel }} — {{ row.assetSerialNumber }}</template>
          </el-table-column>
          <el-table-column label="Desde" width="140">
            <template #default="{ row }">{{ new Date(row.startDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) }}</template>
          </el-table-column>
          <el-table-column label="Hasta" width="140">
            <template #default="{ row }">
              <span v-if="row.endDate">{{ new Date(row.endDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) }}</span>
              <el-tag v-else type="success" size="small">Activo</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="" width="160">
            <template #default="{ row }">
              <el-button v-if="!row.endDate" link @click="endLink(row)">Finalizar vínculo</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-card>
    </template>

    <el-dialog v-model="addAssetDialogVisible" title="Vincular activo" width="420px">
      <el-form :model="addAssetForm" label-position="top">
        <el-form-item label="Activo">
          <el-select v-model="addAssetForm.assetId" style="width: 100%" filterable placeholder="Selecciona un activo en bodega">
            <el-option
              v-for="a in availableAssets"
              :key="a.id"
              :label="`${a.assetBrandName} ${a.model} — ${a.serialNumber}`"
              :value="a.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="Sede de destino">
          <el-select v-model="addAssetForm.clientLocationId" style="width: 100%" filterable placeholder="Selecciona una sede">
            <el-option v-for="l in locations" :key="l.id" :label="l.name" :value="l.id" />
          </el-select>
        </el-form-item>
        <p class="dialog-hint">
          El activo quedará "Pendiente de instalar" en esta sede hasta que el técnico confirme la instalación.
        </p>
      </el-form>
      <template #footer>
        <el-button @click="addAssetDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingAsset" @click="saveAddAsset">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.section-card {
  margin-bottom: 1.5rem;
}

.section-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 1rem;
}

.section-actions {
  display: flex;
  justify-content: flex-end;
}

.dialog-hint {
  color: #6b7280;
  font-size: 0.8rem;
  margin: 0;
}
</style>
