<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import * as assetBrandsApi from '../../api/assetBrands'
import * as assetModelsApi from '../../api/assetModels'
import type { AssetBrandDto, AssetModelDto } from '../../api/types'

const route = useRoute()
const router = useRouter()
const brandId = route.params.id as string

const brand = ref<AssetBrandDto | null>(null)
const models = ref<AssetModelDto[]>([])
const loading = ref(false)
const savingModelId = ref<string | null>(null)

type EditableModel = Omit<AssetModelDto, 'id' | 'assetBrandId'>
const editableModels = ref<Record<string, EditableModel>>({})

const dialogVisible = ref(false)
const saving = ref(false)
const form = reactive({
  name: '',
  generalPrintThreshold: 30000,
  generalMonthsInterval: 6,
  unitsPrintThreshold: 30000,
  unitsMonthsInterval: 6,
  consumablesPrintThreshold: 60000
})

async function loadAll() {
  loading.value = true
  try {
    const [brandsRes, modelsRes] = await Promise.all([
      assetBrandsApi.listAssetBrands(),
      assetModelsApi.listAssetModels(brandId)
    ])
    brand.value = brandsRes.data.find((b) => b.id === brandId) ?? null
    models.value = modelsRes.data
    editableModels.value = Object.fromEntries(
      models.value.map((m) => [
        m.id,
        {
          name: m.name,
          generalPrintThreshold: m.generalPrintThreshold,
          generalMonthsInterval: m.generalMonthsInterval,
          unitsPrintThreshold: m.unitsPrintThreshold,
          unitsMonthsInterval: m.unitsMonthsInterval,
          consumablesPrintThreshold: m.consumablesPrintThreshold
        }
      ])
    )
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar la marca.')
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.name = ''
  form.generalPrintThreshold = 30000
  form.generalMonthsInterval = 6
  form.unitsPrintThreshold = 30000
  form.unitsMonthsInterval = 6
  form.consumablesPrintThreshold = 60000
  dialogVisible.value = true
}

async function handleCreate() {
  saving.value = true
  try {
    await assetModelsApi.createAssetModel(brandId, { ...form })
    ElMessage.success('Modelo creado.')
    dialogVisible.value = false
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear el modelo.')
  } finally {
    saving.value = false
  }
}

async function saveModel(modelId: string) {
  const edited = editableModels.value[modelId]
  if (!edited) return
  savingModelId.value = modelId
  try {
    await assetModelsApi.updateAssetModel(brandId, modelId, { ...edited })
    ElMessage.success('Modelo actualizado.')
    await loadAll()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo actualizar el modelo.')
  } finally {
    savingModelId.value = null
  }
}

onMounted(loadAll)
</script>

<template>
  <div v-loading="loading">
    <template v-if="brand">
      <div class="page-header">
        <el-button :icon="ArrowLeft" circle title="Volver a Marcas" @click="router.push({ name: 'asset-brands' })" />
        <h1>{{ brand.name }}</h1>
      </div>
      <p class="hint">
        Modelos de esta marca y sus umbrales de mantenimiento (impresiones y/o meses). Son constantes por
        modelo, editables a demanda.
      </p>

      <div class="section-actions">
        <el-button type="primary" @click="openCreateDialog">Nuevo modelo</el-button>
      </div>

      <el-table :data="models" stripe>
        <el-table-column label="Nombre" width="180">
          <template #default="{ row }">
            <el-input v-model="editableModels[row.id].name" />
          </template>
        </el-table-column>
        <el-table-column label="General — impresiones" width="150">
          <template #default="{ row }">
            <el-input-number v-model="editableModels[row.id].generalPrintThreshold" :min="1" style="width: 100%" />
          </template>
        </el-table-column>
        <el-table-column label="General — meses" width="130">
          <template #default="{ row }">
            <el-input-number v-model="editableModels[row.id].generalMonthsInterval" :min="1" style="width: 100%" />
          </template>
        </el-table-column>
        <el-table-column label="Unidades — impresiones" width="150">
          <template #default="{ row }">
            <el-input-number v-model="editableModels[row.id].unitsPrintThreshold" :min="1" style="width: 100%" />
          </template>
        </el-table-column>
        <el-table-column label="Unidades — meses" width="130">
          <template #default="{ row }">
            <el-input-number v-model="editableModels[row.id].unitsMonthsInterval" :min="1" style="width: 100%" />
          </template>
        </el-table-column>
        <el-table-column label="Insumos — impresiones" width="150">
          <template #default="{ row }">
            <el-input-number v-model="editableModels[row.id].consumablesPrintThreshold" :min="1" style="width: 100%" />
          </template>
        </el-table-column>
        <el-table-column label="" width="110">
          <template #default="{ row }">
            <el-button type="primary" size="small" :loading="savingModelId === row.id" @click="saveModel(row.id)">
              Guardar
            </el-button>
          </template>
        </el-table-column>
      </el-table>
      <p v-if="!loading && models.length === 0" class="muted">Esta marca todavía no tiene modelos registrados.</p>
    </template>

    <el-dialog v-model="dialogVisible" title="Nuevo modelo" width="480px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Nombre">
          <el-input v-model="form.name" placeholder="Ej: MP2014" />
        </el-form-item>
        <div class="form-grid">
          <el-form-item label="Mantenimiento general — impresiones">
            <el-input-number v-model="form.generalPrintThreshold" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Mantenimiento general — meses">
            <el-input-number v-model="form.generalMonthsInterval" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Mantenimiento de unidades — impresiones">
            <el-input-number v-model="form.unitsPrintThreshold" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Mantenimiento de unidades — meses">
            <el-input-number v-model="form.unitsMonthsInterval" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="Cambio de insumos — impresiones">
            <el-input-number v-model="form.consumablesPrintThreshold" :min="1" style="width: 100%" />
          </el-form-item>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="saving" @click="handleCreate">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.25rem;
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
  padding: 0.5rem 0;
}

.section-actions {
  display: flex;
  justify-content: flex-end;
  margin-bottom: 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 1rem;
}
</style>
