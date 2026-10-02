<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as inventoryApi from '../../api/inventory'
import * as citiesApi from '../../api/cities'
import { useRealtimeUpdates } from '../../composables/useRealtime'
import { useAuthStore } from '../../stores/auth'
import {
  InventoryCategories,
  InventoryCategoryLabels,
  MovementTypeLabels,
  type InventoryItemDto,
  type InventoryLocationDto,
  type InventoryMovementDto,
  type StockRowDto
} from '../../api/inventory'
import { RoleNames, type CityDto } from '../../api/types'

const auth = useAuthStore()
const isAdmin = computed(() => auth.hasRole(RoleNames.Administrador))

const tab = ref('stock')
const locations = ref<InventoryLocationDto[]>([])
const cities = ref<CityDto[]>([])

// ── Existencias ────────────────────────────────────────────────────────────────────────────────
const stock = ref<StockRowDto[]>([])
const stockTotal = ref(0)
const stockPage = ref(1)
const stockPageSize = 20
const stockLoading = ref(false)
const stockFilter = reactive({ locationId: '', category: '', onlyLow: false })

async function loadStock(silent = false) {
  if (!silent) stockLoading.value = true
  try {
    const { data } = await inventoryApi.listStock({
      locationId: stockFilter.locationId || undefined,
      category: stockFilter.category || undefined,
      onlyLow: stockFilter.onlyLow || undefined,
      page: stockPage.value,
      pageSize: stockPageSize
    })
    stock.value = data.items
    stockTotal.value = data.totalCount ?? data.items.length
  } catch (err: any) {
    console.error('InventoryView.loadStock failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las existencias.')
  } finally {
    stockLoading.value = false
  }
}

// ── Movimientos ────────────────────────────────────────────────────────────────────────────────
const movements = ref<InventoryMovementDto[]>([])
const movementsCursor = ref<string | null>(null)
const movementsLoading = ref(false)
const movementFilter = reactive({ locationId: '' })

async function loadMovements(more = false) {
  movementsLoading.value = true
  try {
    const { data } = await inventoryApi.listMovements({
      locationId: movementFilter.locationId || undefined,
      cursor: more ? movementsCursor.value ?? undefined : undefined,
      pageSize: 25
    })
    movements.value = more ? [...movements.value, ...data.items] : data.items
    movementsCursor.value = data.hasMore ? data.nextCursor ?? null : null
  } catch (err: any) {
    console.error('InventoryView.loadMovements failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar los movimientos.')
  } finally {
    movementsLoading.value = false
  }
}

// ── Catálogo ───────────────────────────────────────────────────────────────────────────────────
const items = ref<InventoryItemDto[]>([])
const itemsTotal = ref(0)
const itemsPage = ref(1)
const itemsLoading = ref(false)
const itemFilter = reactive({ search: '', category: '' })

async function loadItems() {
  itemsLoading.value = true
  try {
    const { data } = await inventoryApi.listItems({
      search: itemFilter.search || undefined,
      category: itemFilter.category || undefined,
      page: itemsPage.value,
      pageSize: 20
    })
    items.value = data.items
    itemsTotal.value = data.totalCount ?? data.items.length
  } catch (err: any) {
    console.error('InventoryView.loadItems failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el catálogo.')
  } finally {
    itemsLoading.value = false
  }
}

const itemDialogVisible = ref(false)
const editingItemId = ref<string | null>(null)
const savingItem = ref(false)
// Sin precio unitario por ahora (decisión de negocio): el campo existe en la base pero no se captura ni se muestra.
const itemForm = reactive({ name: '', category: 'ConsumibleBase' as inventoryApi.InventoryCategory, unit: '', minimumStock: 0, isActive: true })

function openItemDialog(item: InventoryItemDto | null) {
  editingItemId.value = item?.id ?? null
  itemForm.name = item?.name ?? ''
  itemForm.category = item?.category ?? 'ConsumibleBase'
  itemForm.unit = item?.unit ?? ''
  itemForm.minimumStock = item?.minimumStock ?? 0
  itemForm.isActive = item?.isActive ?? true
  itemDialogVisible.value = true
}

async function saveItem() {
  if (!itemForm.name.trim()) return
  savingItem.value = true
  const request = {
    name: itemForm.name.trim(),
    category: itemForm.category,
    unit: itemForm.unit.trim() || null,
    minimumStock: itemForm.minimumStock,
    isActive: itemForm.isActive
  }
  try {
    if (editingItemId.value) await inventoryApi.updateItem(editingItemId.value, request)
    else await inventoryApi.createItem(request)
    ElMessage.success('Ítem guardado.')
    itemDialogVisible.value = false
    await loadItems()
  } catch (err: any) {
    console.error('InventoryView.saveItem failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo guardar el ítem.')
  } finally {
    savingItem.value = false
  }
}

// ── Operaciones de stock (entrada / traspaso / ajuste) ─────────────────────────────────────────
type Operation = 'entry' | 'transfer' | 'adjust'
const opDialogVisible = ref(false)
const opKind = ref<Operation>('entry')
const savingOp = ref(false)
const opForm = reactive({ itemId: '', locationId: '', toLocationId: '', quantity: 1, delta: 1, notes: '' })
const itemOptions = ref<InventoryItemDto[]>([])
const searchingItems = ref(false)

const opTitle = computed(() => ({ entry: 'Registrar entrada', transfer: 'Traspasar entre ubicaciones', adjust: 'Ajustar saldo' })[opKind.value])
const opValid = computed(() => {
  if (!opForm.itemId || !opForm.locationId) return false
  if (opKind.value === 'transfer') return !!opForm.toLocationId && opForm.toLocationId !== opForm.locationId && opForm.quantity >= 1
  if (opKind.value === 'adjust') return opForm.delta !== 0 && !!opForm.notes.trim()
  return opForm.quantity >= 1
})

async function searchItems(query: string) {
  searchingItems.value = true
  try {
    const { data } = await inventoryApi.listItems({ search: query || undefined, activeOnly: true, pageSize: 20 })
    itemOptions.value = data.items
  } catch (err) {
    console.error('InventoryView.searchItems failed', err)
  } finally {
    searchingItems.value = false
  }
}

function openOperation(kind: Operation) {
  opKind.value = kind
  opForm.itemId = ''
  opForm.locationId = kind === 'entry' ? locations.value.find((l) => l.kind === 'Principal')?.id ?? '' : stockFilter.locationId
  opForm.toLocationId = ''
  opForm.quantity = 1
  opForm.delta = 1
  opForm.notes = ''
  void searchItems('')
  opDialogVisible.value = true
}

async function saveOperation() {
  if (!opValid.value) return
  savingOp.value = true
  try {
    if (opKind.value === 'entry') {
      await inventoryApi.registerEntry({ locationId: opForm.locationId, itemId: opForm.itemId, quantity: opForm.quantity, notes: opForm.notes || null })
    } else if (opKind.value === 'transfer') {
      await inventoryApi.transfer({ itemId: opForm.itemId, fromLocationId: opForm.locationId, toLocationId: opForm.toLocationId, quantity: opForm.quantity, notes: opForm.notes || null })
    } else {
      await inventoryApi.adjust({ locationId: opForm.locationId, itemId: opForm.itemId, delta: opForm.delta, notes: opForm.notes.trim() })
    }
    ElMessage.success('Movimiento registrado.')
    opDialogVisible.value = false
    await Promise.all([loadStock(true), loadMovements()])
  } catch (err: any) {
    console.error('InventoryView.saveOperation failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo registrar el movimiento.')
  } finally {
    savingOp.value = false
  }
}

// ── Ubicaciones ────────────────────────────────────────────────────────────────────────────────
const mainLocation = computed(() => locations.value.find((l) => l.kind === 'Principal') ?? null)
const zoneLocations = computed(() => locations.value.filter((l) => l.kind === 'Zona'))
const mainForm = reactive({ name: '', address: '', cityId: '' })
const savingMain = ref(false)

function syncMainForm() {
  mainForm.name = mainLocation.value?.name ?? ''
  mainForm.address = mainLocation.value?.address ?? ''
  mainForm.cityId = mainLocation.value?.cityId ?? ''
}

async function saveMain() {
  if (!mainForm.name.trim()) return
  savingMain.value = true
  try {
    await inventoryApi.updateMainLocation({ name: mainForm.name.trim(), address: mainForm.address.trim() || null, cityId: mainForm.cityId || null })
    ElMessage.success('Sede principal actualizada.')
    await loadLocations()
  } catch (err: any) {
    console.error('InventoryView.saveMain failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo actualizar la sede principal.')
  } finally {
    savingMain.value = false
  }
}

async function loadLocations() {
  try {
    const [locRes, cityRes] = await Promise.all([inventoryApi.listLocations(), cities.value.length ? Promise.resolve(null) : citiesApi.listCities()])
    locations.value = locRes.data
    if (cityRes) cities.value = cityRes.data
    syncMainForm()
  } catch (err: any) {
    console.error('InventoryView.loadLocations failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las ubicaciones.')
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
}

watch(() => [stockFilter.locationId, stockFilter.category, stockFilter.onlyLow], () => {
  stockPage.value = 1
  void loadStock()
})
watch(() => movementFilter.locationId, () => void loadMovements())
watch(() => [itemFilter.search, itemFilter.category], () => {
  itemsPage.value = 1
  void loadItems()
})

onMounted(async () => {
  await loadLocations()
  await Promise.all([loadStock(), loadMovements(), loadItems()])
})

useRealtimeUpdates(['Inventory', 'Zone'], () => {
  void loadLocations()
  void loadStock(true)
  void loadMovements()
  void loadItems()
})
</script>

<template>
  <div>
    <div class="page-header">
      <div>
        <h1>Inventario</h1>
        <p class="muted">Bodega principal e inventario por zona. Las compras entran a la principal y se traspasan a las zonas; lo que se consuma en un municipio descuenta de su zona.</p>
      </div>
    </div>

    <el-tabs v-model="tab">
      <el-tab-pane label="Existencias" name="stock">
        <div class="toolbar">
          <el-select v-model="stockFilter.locationId" clearable placeholder="Todas las ubicaciones" style="width: 220px">
            <el-option v-for="l in locations" :key="l.id" :label="l.name" :value="l.id" />
          </el-select>
          <el-select v-model="stockFilter.category" clearable placeholder="Toda categoría" style="width: 180px">
            <el-option v-for="c in InventoryCategories" :key="c" :label="InventoryCategoryLabels[c]" :value="c" />
          </el-select>
          <el-switch v-model="stockFilter.onlyLow" active-text="Solo stock bajo" />
          <span class="spacer" />
          <el-button type="primary" @click="openOperation('entry')">Registrar entrada</el-button>
          <el-button @click="openOperation('transfer')">Traspasar</el-button>
          <el-button @click="openOperation('adjust')">Ajustar</el-button>
        </div>
        <el-table v-loading="stockLoading" :data="stock" stripe empty-text="Sin existencias registradas todavía. Registra una entrada para empezar.">
          <el-table-column prop="itemName" label="Ítem" min-width="200" />
          <el-table-column label="Categoría" width="150">
            <template #default="{ row }">{{ InventoryCategoryLabels[row.category as inventoryApi.InventoryCategory] }}</template>
          </el-table-column>
          <el-table-column prop="locationName" label="Ubicación" width="220" />
          <el-table-column label="Saldo" width="130">
            <template #default="{ row }">
              <el-tag :type="row.isLow ? 'danger' : 'success'" size="small">{{ row.quantity }}</el-tag>
              <span v-if="row.isLow" class="low-hint">{{ row.quantity < 0 ? 'negativo' : 'bajo' }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="minimumStock" label="Mínimo" width="100" />
        </el-table>
        <el-pagination v-model:current-page="stockPage" class="pager" layout="prev, pager, next" :page-size="stockPageSize" :total="stockTotal" @current-change="loadStock()" />
      </el-tab-pane>

      <el-tab-pane label="Movimientos" name="movements">
        <div class="toolbar">
          <el-select v-model="movementFilter.locationId" clearable placeholder="Todas las ubicaciones" style="width: 220px">
            <el-option v-for="l in locations" :key="l.id" :label="l.name" :value="l.id" />
          </el-select>
        </div>
        <el-table v-loading="movementsLoading && movements.length === 0" :data="movements" stripe empty-text="Sin movimientos.">
          <el-table-column label="Fecha" width="190">
            <template #default="{ row }">{{ formatDate(row.occurredAt) }}</template>
          </el-table-column>
          <el-table-column prop="itemName" label="Ítem" min-width="180" />
          <el-table-column prop="locationName" label="Ubicación" width="200" />
          <el-table-column label="Tipo" width="150">
            <template #default="{ row }">{{ MovementTypeLabels[row.type as keyof typeof MovementTypeLabels] }}</template>
          </el-table-column>
          <el-table-column label="Cantidad" width="110">
            <template #default="{ row }">
              <span :class="row.delta < 0 ? 'neg' : 'pos'">{{ row.delta > 0 ? '+' : '' }}{{ row.delta }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="createdByUserName" label="Registró" width="170" />
          <el-table-column prop="notes" label="Notas" min-width="160" />
        </el-table>
        <div v-if="movementsCursor" class="more">
          <el-button :loading="movementsLoading" @click="loadMovements(true)">Cargar más</el-button>
        </div>
      </el-tab-pane>

      <el-tab-pane label="Catálogo" name="items">
        <div class="toolbar">
          <el-input v-model="itemFilter.search" clearable placeholder="Buscar ítem" style="width: 240px" />
          <el-select v-model="itemFilter.category" clearable placeholder="Toda categoría" style="width: 180px">
            <el-option v-for="c in InventoryCategories" :key="c" :label="InventoryCategoryLabels[c]" :value="c" />
          </el-select>
          <span class="spacer" />
          <el-button type="primary" @click="openItemDialog(null)">Nuevo ítem</el-button>
        </div>
        <el-table v-loading="itemsLoading" :data="items" stripe empty-text="El catálogo está vacío.">
          <el-table-column prop="name" label="Nombre" min-width="220" />
          <el-table-column label="Categoría" width="150">
            <template #default="{ row }">{{ InventoryCategoryLabels[row.category as inventoryApi.InventoryCategory] }}</template>
          </el-table-column>
          <el-table-column prop="unit" label="Unidad" width="110" />
          <el-table-column prop="minimumStock" label="Mínimo" width="100" />
          <el-table-column label="Estado" width="110">
            <template #default="{ row }"><el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? 'Activo' : 'Inactivo' }}</el-tag></template>
          </el-table-column>
          <el-table-column label="" width="100">
            <template #default="{ row }"><el-button link @click="openItemDialog(row)">Editar</el-button></template>
          </el-table-column>
        </el-table>
        <el-pagination v-model:current-page="itemsPage" class="pager" layout="prev, pager, next" :page-size="20" :total="itemsTotal" @current-change="loadItems" />
      </el-tab-pane>

      <el-tab-pane label="Ubicaciones" name="locations">
        <el-card class="main-card">
          <template #header>Sede principal y bodega principal</template>
          <el-form label-position="top" :disabled="!isAdmin">
            <el-form-item label="Nombre"><el-input v-model="mainForm.name" maxlength="150" /></el-form-item>
            <el-form-item label="Dirección"><el-input v-model="mainForm.address" maxlength="300" /></el-form-item>
            <el-form-item label="Municipio">
              <el-select v-model="mainForm.cityId" filterable clearable placeholder="Selecciona" style="width: 100%">
                <el-option v-for="c in cities" :key="c.id" :label="`${c.name} (${c.stateOrProvince})`" :value="c.id" />
              </el-select>
            </el-form-item>
          </el-form>
          <el-button v-if="isAdmin" type="primary" :loading="savingMain" :disabled="!mainForm.name.trim()" @click="saveMain">Guardar</el-button>
          <p v-else class="muted">Solo el Administrador puede cambiar la sede principal.</p>
        </el-card>
        <h3>Inventario por zona</h3>
        <p class="muted">Cada zona tiene su inventario, creado con la zona. Se gestiona desde la sección Zonas.</p>
        <el-table :data="zoneLocations" stripe empty-text="Todavía no hay zonas.">
          <el-table-column prop="name" label="Zona" />
        </el-table>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="opDialogVisible" :title="opTitle" width="480px">
      <el-form label-position="top">
        <el-form-item label="Ítem">
          <el-select v-model="opForm.itemId" filterable remote :remote-method="searchItems" :loading="searchingItems" placeholder="Busca un ítem" style="width: 100%">
            <el-option v-for="i in itemOptions" :key="i.id" :label="i.name" :value="i.id" />
          </el-select>
        </el-form-item>
        <el-form-item :label="opKind === 'transfer' ? 'Desde' : 'Ubicación'">
          <el-select v-model="opForm.locationId" style="width: 100%">
            <el-option v-for="l in locations" :key="l.id" :label="l.name" :value="l.id" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="opKind === 'transfer'" label="Hacia">
          <el-select v-model="opForm.toLocationId" style="width: 100%">
            <el-option v-for="l in locations.filter((x) => x.id !== opForm.locationId)" :key="l.id" :label="l.name" :value="l.id" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="opKind !== 'adjust'" label="Cantidad">
          <el-input-number v-model="opForm.quantity" :min="1" :max="100000" style="width: 100%" />
        </el-form-item>
        <el-form-item v-else label="Ajuste (+ suma, − resta)">
          <el-input-number v-model="opForm.delta" :min="-100000" :max="100000" style="width: 100%" />
        </el-form-item>
        <el-form-item :label="opKind === 'adjust' ? 'Motivo (obligatorio)' : 'Notas (opcional)'">
          <el-input v-model="opForm.notes" type="textarea" :rows="2" maxlength="500" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="opDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :disabled="!opValid" :loading="savingOp" @click="saveOperation">Registrar</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="itemDialogVisible" :title="editingItemId ? 'Editar ítem' : 'Nuevo ítem'" width="480px">
      <el-form label-position="top">
        <el-form-item label="Nombre"><el-input v-model="itemForm.name" maxlength="150" placeholder="Ej: Fusor" /></el-form-item>
        <el-form-item label="Categoría">
          <el-select v-model="itemForm.category" style="width: 100%">
            <el-option v-for="c in InventoryCategories" :key="c" :label="InventoryCategoryLabels[c]" :value="c" />
          </el-select>
        </el-form-item>
        <div class="form-grid">
          <el-form-item label="Unidad"><el-input v-model="itemForm.unit" maxlength="30" placeholder="und" /></el-form-item>
          <el-form-item label="Stock mínimo (0 = sin alerta)"><el-input-number v-model="itemForm.minimumStock" :min="0" style="width: 100%" /></el-form-item>
          <el-form-item v-if="editingItemId" label="Activo"><el-switch v-model="itemForm.isActive" /></el-form-item>
        </div>
      </el-form>
      <template #footer>
        <el-button @click="itemDialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :disabled="!itemForm.name.trim()" :loading="savingItem" @click="saveItem">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header h1 { margin: 0 0 0.25rem; }
.page-header p { margin: 0 0 1rem; }
.toolbar { display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center; margin-bottom: 1rem; }
.spacer { flex: 1; }
.pager { margin-top: 1rem; justify-content: flex-end; }
.more { text-align: center; margin-top: 1rem; }
.low-hint { margin-left: 0.4rem; font-size: 0.75rem; color: var(--el-color-danger); }
.pos { color: var(--el-color-success); font-weight: 600; }
.neg { color: var(--el-color-danger); font-weight: 600; }
.main-card { max-width: 560px; margin-bottom: 1.5rem; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 1rem; }
</style>
