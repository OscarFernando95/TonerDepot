<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as inventoryApi from '../../api/inventory'
import type { PartOptionDto, VisitKitDto } from '../../api/inventory'

// Piezas cambiadas en una visita. En una orden que incluye cambio de consumibles muestra el kit base del modelo
// (todo marcado por defecto; desmarcar = no se cambió) y, en cualquier ticket u orden, permite agregar repuestos del
// inventario de la zona. Nunca bloquea el cierre: si falta stock solo avisa (el servidor deja un aviso de negativo).
const props = defineProps<{ assetId: string | null; includeKit: boolean }>()
const parts = defineModel<{ itemId: string; quantity: number }[]>({ default: () => [] })

interface Row {
  itemId: string
  name: string
  groupName: string
  quantity: number
  stock: number
  checked: boolean
  fromKit: boolean
}

const kit = ref<VisitKitDto | null>(null)
const rows = ref<Row[]>([])
const loading = ref(false)

const options = ref<PartOptionDto[]>([])
const searching = ref(false)
const picked = reactive({ itemId: '', quantity: 1 })


const groups = computed(() => {
  const map = new Map<string, Row[]>()
  for (const r of rows.value.filter((x) => x.fromKit)) {
    if (!map.has(r.groupName)) map.set(r.groupName, [])
    map.get(r.groupName)!.push(r)
  }
  return [...map.entries()]
})
const extras = computed(() => rows.value.filter((r) => !r.fromKit))
const locationName = computed(() => kit.value?.locationName ?? '')

async function load() {
  rows.value = []
  kit.value = null
  if (!props.includeKit || !props.assetId) {
    sync()
    return
  }
  loading.value = true
  try {
    const { data } = await inventoryApi.getVisitKit(props.assetId)
    kit.value = data
    rows.value = data.items.map((i) => ({
      itemId: i.itemId, name: i.itemName, groupName: i.groupName, quantity: i.quantity, stock: i.stock, checked: true, fromKit: true
    }))
  } catch (err: any) {
    console.error('VisitPartsPicker.load failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el kit de consumibles.')
  } finally {
    loading.value = false
    sync()
  }
}

async function searchParts(query: string) {
  searching.value = true
  try {
    const { data } = await inventoryApi.searchParts({ assetId: props.assetId, search: query || undefined, pageSize: 20 })
    options.value = data.items
  } catch (err) {
    console.error('VisitPartsPicker.searchParts failed', err)
  } finally {
    searching.value = false
  }
}

function addExtra() {
  const option = options.value.find((o) => o.itemId === picked.itemId)
  if (!option) return
  const existing = rows.value.find((r) => r.itemId === option.itemId)
  if (existing) {
    existing.checked = true
    existing.quantity += picked.quantity
  } else {
    rows.value.push({ itemId: option.itemId, name: option.name, groupName: '', quantity: picked.quantity, stock: option.stock, checked: true, fromKit: false })
  }
  picked.itemId = ''
  picked.quantity = 1
  sync()
}

function removeExtra(row: Row) {
  rows.value = rows.value.filter((r) => r !== row)
  sync()
}

function sync() {
  parts.value = rows.value.filter((r) => r.checked && r.quantity > 0).map((r) => ({ itemId: r.itemId, quantity: r.quantity }))
}

watch(() => [props.assetId, props.includeKit], load, { immediate: true })
void searchParts('')
</script>

<template>
  <div v-loading="loading" class="parts-picker">
    <template v-if="includeKit && assetId">
      <p v-if="kit" class="hint">
        Se descuenta de: <strong>{{ locationName }}</strong>
        <span v-if="kit.usesMainWarehouse"> (el municipio del equipo no tiene zona: bodega principal)</span>
      </p>
      <p v-if="kit && kit.items.length === 0" class="muted">Este modelo no tiene kit base definido (ni la marca).</p>
      <div v-for="[group, items] in groups" :key="group" class="group">
        <div class="group-title">{{ group }}</div>
        <div v-for="row in items" :key="row.itemId" class="row">
          <el-checkbox v-model="row.checked" @change="sync">{{ row.name }}</el-checkbox>
          <el-input-number v-model="row.quantity" :min="1" :max="100" size="small" :disabled="!row.checked" @change="sync" />
          <el-tag size="small" :type="row.stock < row.quantity ? 'danger' : 'info'">stock {{ row.stock }}</el-tag>
        </div>
      </div>
    </template>

    <div v-if="extras.length" class="group">
      <div class="group-title">Repuestos agregados</div>
      <div v-for="row in extras" :key="row.itemId" class="row">
        <span class="name">{{ row.name }}</span>
        <el-input-number v-model="row.quantity" :min="1" :max="100" size="small" @change="sync" />
        <el-tag size="small" :type="row.stock < row.quantity ? 'danger' : 'info'">stock {{ row.stock }}</el-tag>
        <el-button link type="danger" size="small" @click="removeExtra(row)">Quitar</el-button>
      </div>
    </div>

    <div class="add-row">
      <el-select v-model="picked.itemId" filterable remote :remote-method="searchParts" :loading="searching" placeholder="Agregar repuesto (busca en el inventario)" style="flex: 1">
        <el-option v-for="o in options" :key="o.itemId" :label="o.name" :value="o.itemId">
          <span>{{ o.name }}</span>
          <span class="option-stock" :class="{ low: o.stock <= 0 }">stock {{ o.stock }}</span>
        </el-option>
      </el-select>
      <el-input-number v-model="picked.quantity" :min="1" :max="100" />
      <el-button :disabled="!picked.itemId" @click="addExtra">Agregar</el-button>
    </div>
    <p class="hint">Si una pieza no tiene stock en la zona, igual se registra el cambio y queda un aviso para reponerla.</p>
  </div>
</template>

<style scoped>
.parts-picker { width: 100%; }
.hint { margin: 0.25rem 0 0.5rem; font-size: 0.8rem; color: var(--el-text-color-secondary); }
.muted { color: var(--el-text-color-secondary); }
.group { margin-bottom: 0.75rem; }
.group-title { font-weight: 600; margin-bottom: 0.25rem; }
.row { display: flex; align-items: center; gap: 0.75rem; padding: 0.15rem 0; }
.row .name { flex: 1; }
.row :deep(.el-checkbox) { flex: 1; }
.add-row { display: flex; gap: 0.5rem; align-items: center; margin-top: 0.5rem; }
.option-stock { float: right; font-size: 0.75rem; color: var(--el-text-color-secondary); }
.option-stock.low { color: var(--el-color-danger); }
</style>
