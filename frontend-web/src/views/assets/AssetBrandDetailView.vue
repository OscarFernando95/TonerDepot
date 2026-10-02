<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { ArrowLeft } from '@element-plus/icons-vue'
import * as assetBrandsApi from '../../api/assetBrands'
import * as assetModelsApi from '../../api/assetModels'
import * as inventoryApi from '../../api/inventory'
import type { AssetBrandDto, AssetModelDto } from '../../api/types'
import type { InventoryItemDto, ModelKitItemDto } from '../../api/inventory'

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

// ── Kit base de consumibles ────────────────────────────────────────────────────────────────────
// Se define por marca; cada modelo lo hereda y lo puede ajustar (excluir, cambiar cantidad/grupo, agregar).
interface KitRow {
  itemId: string
  itemName: string
  groupName: string
  quantity: number
}

const brandKit = ref<KitRow[]>([])
const savingKit = ref(false)
const itemOptions = ref<InventoryItemDto[]>([])
const searchingItems = ref(false)
const kitGroups = computed(() => [...new Set(brandKit.value.map((r) => r.groupName).filter(Boolean))])

async function searchItems(query: string) {
  searchingItems.value = true
  try {
    const { data } = await inventoryApi.listItems({ search: query || undefined, activeOnly: true, pageSize: 20 })
    itemOptions.value = data.items
  } catch (err) {
    console.error('AssetBrandDetailView.searchItems failed', err)
  } finally {
    searchingItems.value = false
  }
}

function toRows(data: { itemId: string; itemName: string; groupName: string; quantity: number }[]): KitRow[] {
  return data.map((k) => ({ itemId: k.itemId, itemName: k.itemName, groupName: k.groupName, quantity: k.quantity }))
}

async function loadBrandKit() {
  try {
    const { data } = await inventoryApi.getBrandKit(brandId)
    brandKit.value = toRows(data)
  } catch (err: any) {
    console.error('AssetBrandDetailView.loadBrandKit failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el kit base.')
  }
}

const newKitItem = reactive({ itemId: '', groupName: '', quantity: 1 })

function addKitItem() {
  if (!newKitItem.itemId || !newKitItem.groupName.trim()) return
  if (brandKit.value.some((r) => r.itemId === newKitItem.itemId)) {
    ElMessage.warning('Ese ítem ya está en el kit.')
    return
  }
  const option = itemOptions.value.find((i) => i.id === newKitItem.itemId)
  brandKit.value.push({ itemId: newKitItem.itemId, itemName: option?.name ?? '', groupName: newKitItem.groupName.trim(), quantity: newKitItem.quantity })
  newKitItem.itemId = ''
  newKitItem.quantity = 1
}

function removeKitItem(row: KitRow) {
  brandKit.value = brandKit.value.filter((r) => r.itemId !== row.itemId)
}

async function saveBrandKit() {
  savingKit.value = true
  try {
    const { data } = await inventoryApi.setBrandKit(
      brandId,
      brandKit.value.map((r) => ({ itemId: r.itemId, groupName: r.groupName.trim(), quantity: r.quantity }))
    )
    brandKit.value = toRows(data)
    ElMessage.success('Kit base de la marca guardado.')
  } catch (err: any) {
    console.error('AssetBrandDetailView.saveBrandKit failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo guardar el kit base.')
  } finally {
    savingKit.value = false
  }
}

// Kit efectivo de un modelo: lo heredado de la marca + sus ajustes.
interface ModelKitRow extends ModelKitItemDto {
  fromBrand: boolean
  brandGroup: string
  brandQuantity: number
}

const kitDialogVisible = ref(false)
const kitModel = ref<AssetModelDto | null>(null)
const modelKit = ref<ModelKitRow[]>([])
const loadingModelKit = ref(false)
const savingModelKit = ref(false)
const addModelItem = reactive({ itemId: '', groupName: '', quantity: 1 })

async function openModelKit(model: AssetModelDto) {
  kitModel.value = model
  kitDialogVisible.value = true
  loadingModelKit.value = true
  try {
    const { data } = await inventoryApi.getModelKit(brandId, model.id)
    const brandById = new Map(brandKit.value.map((r) => [r.itemId, r]))
    modelKit.value = data.map((k) => {
      const b = brandById.get(k.itemId)
      return { ...k, fromBrand: !!b, brandGroup: b?.groupName ?? '', brandQuantity: b?.quantity ?? 1 }
    })
  } catch (err: any) {
    console.error('AssetBrandDetailView.openModelKit failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el kit del modelo.')
  } finally {
    loadingModelKit.value = false
  }
}

function addToModel() {
  if (!addModelItem.itemId || !addModelItem.groupName.trim()) return
  if (modelKit.value.some((r) => r.itemId === addModelItem.itemId)) {
    ElMessage.warning('Ese ítem ya está en el kit del modelo.')
    return
  }
  const option = itemOptions.value.find((i) => i.id === addModelItem.itemId)
  modelKit.value.push({
    itemId: addModelItem.itemId, itemName: option?.name ?? '', category: option?.category ?? 'Repuesto',
    groupName: addModelItem.groupName.trim(), quantity: addModelItem.quantity, source: 'Modelo', excluded: false,
    fromBrand: false, brandGroup: '', brandQuantity: 1
  })
  addModelItem.itemId = ''
  addModelItem.quantity = 1
}

function removeModelOnly(row: ModelKitRow) {
  modelKit.value = modelKit.value.filter((r) => r.itemId !== row.itemId)
}

async function saveModelKit() {
  if (!kitModel.value) return
  // Solo se mandan los ajustes: lo heredado sin tocar no genera fila.
  const overrides = modelKit.value
    .filter((r) => !r.fromBrand || r.excluded || r.groupName !== r.brandGroup || r.quantity !== r.brandQuantity)
    .map((r) => ({
      itemId: r.itemId,
      excluded: r.excluded,
      groupName: !r.fromBrand || r.groupName !== r.brandGroup ? r.groupName : null,
      quantity: !r.fromBrand || r.quantity !== r.brandQuantity ? r.quantity : null
    }))
  savingModelKit.value = true
  try {
    await inventoryApi.setModelKit(brandId, kitModel.value.id, overrides)
    ElMessage.success('Kit del modelo guardado.')
    kitDialogVisible.value = false
  } catch (err: any) {
    console.error('AssetBrandDetailView.saveModelKit failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo guardar el kit del modelo.')
  } finally {
    savingModelKit.value = false
  }
}

onMounted(async () => {
  await loadAll()
  await loadBrandKit()
  await searchItems('')
})
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
        <el-table-column label="" width="190">
          <template #default="{ row }">
            <el-button type="primary" size="small" :loading="savingModelId === row.id" @click="saveModel(row.id)">
              Guardar
            </el-button>
            <el-button size="small" @click="openModelKit(row)">Kit</el-button>
          </template>
        </el-table-column>
      </el-table>
      <p v-if="!loading && models.length === 0" class="muted">Esta marca todavía no tiene modelos registrados.</p>

      <el-card class="kit-card">
        <template #header>Kit base de consumibles de la marca</template>
        <p class="hint">
          Piezas que se revisan en cada cambio de consumibles, agrupadas por unidad (por ejemplo, "Unidad fusora").
          Todos los modelos de la marca lo heredan y cada uno puede ajustarlo con el botón Kit.
        </p>
        <el-table :data="brandKit" stripe size="small" empty-text="La marca todavía no tiene kit base.">
          <el-table-column prop="itemName" label="Ítem" min-width="180" />
          <el-table-column label="Unidad / grupo" width="220">
            <template #default="{ row }">
              <el-autocomplete
                v-model="row.groupName"
                :fetch-suggestions="(q: string, cb: (r: { value: string }[]) => void) => cb(kitGroups.filter((g) => g.toLowerCase().includes(q.toLowerCase())).map((g) => ({ value: g })))"
                style="width: 100%"
              />
            </template>
          </el-table-column>
          <el-table-column label="Cantidad" width="130">
            <template #default="{ row }"><el-input-number v-model="row.quantity" :min="1" :max="100" size="small" /></template>
          </el-table-column>
          <el-table-column label="" width="90">
            <template #default="{ row }"><el-button link type="danger" @click="removeKitItem(row)">Quitar</el-button></template>
          </el-table-column>
        </el-table>
        <div class="kit-add">
          <el-select v-model="newKitItem.itemId" filterable remote :remote-method="searchItems" :loading="searchingItems" placeholder="Agregar ítem del catálogo" style="flex: 2">
            <el-option v-for="i in itemOptions" :key="i.id" :label="i.name" :value="i.id" />
          </el-select>
          <el-input v-model="newKitItem.groupName" placeholder="Unidad / grupo" style="flex: 1" />
          <el-input-number v-model="newKitItem.quantity" :min="1" :max="100" />
          <el-button :disabled="!newKitItem.itemId || !newKitItem.groupName.trim()" @click="addKitItem">Agregar</el-button>
        </div>
        <div class="kit-actions">
          <el-button type="primary" :loading="savingKit" :disabled="brandKit.some((r) => !r.groupName.trim())" @click="saveBrandKit">
            Guardar kit de la marca
          </el-button>
        </div>
      </el-card>
    </template>

    <el-dialog v-model="kitDialogVisible" :title="`Kit base — ${kitModel?.name}`" width="680px">
      <div v-loading="loadingModelKit">
        <p class="hint">Hereda el kit de la marca. Desmarca lo que este modelo no usa, ajusta cantidades o agrega piezas solo para este modelo.</p>
        <el-table :data="modelKit" stripe size="small" empty-text="Ni la marca ni el modelo tienen kit base.">
          <el-table-column label="Incluir" width="80">
            <template #default="{ row }"><el-checkbox :model-value="!row.excluded" @change="(v: boolean | string | number) => (row.excluded = !v)" /></template>
          </el-table-column>
          <el-table-column prop="itemName" label="Ítem" min-width="160" />
          <el-table-column label="Unidad / grupo" width="180">
            <template #default="{ row }"><el-input v-model="row.groupName" :disabled="row.excluded" /></template>
          </el-table-column>
          <el-table-column label="Cant." width="120">
            <template #default="{ row }"><el-input-number v-model="row.quantity" :min="1" :max="100" size="small" :disabled="row.excluded" /></template>
          </el-table-column>
          <el-table-column label="Origen" width="130">
            <template #default="{ row }">
              <el-tag size="small" :type="row.fromBrand && row.groupName === row.brandGroup && row.quantity === row.brandQuantity && !row.excluded ? 'info' : 'warning'">
                {{ !row.fromBrand ? 'Solo este modelo' : row.excluded || row.groupName !== row.brandGroup || row.quantity !== row.brandQuantity ? 'Ajustado' : 'Heredado' }}
              </el-tag>
              <el-button v-if="!row.fromBrand" link type="danger" size="small" @click="removeModelOnly(row)">Quitar</el-button>
            </template>
          </el-table-column>
        </el-table>
        <div class="kit-add">
          <el-select v-model="addModelItem.itemId" filterable remote :remote-method="searchItems" :loading="searchingItems" placeholder="Agregar solo a este modelo" style="flex: 2">
            <el-option v-for="i in itemOptions" :key="i.id" :label="i.name" :value="i.id" />
          </el-select>
          <el-input v-model="addModelItem.groupName" placeholder="Unidad / grupo" style="flex: 1" />
          <el-input-number v-model="addModelItem.quantity" :min="1" :max="100" />
          <el-button :disabled="!addModelItem.itemId || !addModelItem.groupName.trim()" @click="addToModel">Agregar</el-button>
        </div>
      </div>
      <template #footer>
        <el-button @click="kitDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="savingModelKit" @click="saveModelKit">Guardar</el-button>
      </template>
    </el-dialog>

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
.kit-card {
  margin-top: 1.5rem;
}

.kit-add {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin-top: 0.75rem;
}

.kit-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 0.75rem;
}

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
