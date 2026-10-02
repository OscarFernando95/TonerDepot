<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as inventoryApi from '../../api/inventory'
import type { PartOptionDto, TonerEntryDto } from '../../api/inventory'

// Tóner entregado o cambiado en una máquina: sirve para saber cuántos gasta cada máquina en un rango de fechas. La
// fecha por defecto es la del momento del registro. Un técnico solo puede en las máquinas que tiene vinculadas.
const visible = ref(false)
const asset = ref<{ id: string; label: string } | null>(null)
const saving = ref(false)
const loadingHistory = ref(false)
const history = ref<TonerEntryDto[]>([])
const options = ref<PartOptionDto[]>([])
const searching = ref(false)

const form = reactive({ itemId: '', quantity: 1, deliveredToUser: false, occurredAt: new Date() as Date | null, counterValue: undefined as number | undefined, notes: '' })

const emit = defineEmits<{ saved: [] }>()

async function searchToner(query: string) {
  searching.value = true
  try {
    const { data } = await inventoryApi.searchParts({ assetId: asset.value?.id, search: query || undefined, category: 'Toner', pageSize: 20 })
    options.value = data.items
  } catch (err) {
    console.error('TonerDialog.searchToner failed', err)
  } finally {
    searching.value = false
  }
}

async function loadHistory() {
  if (!asset.value) return
  loadingHistory.value = true
  try {
    const { data } = await inventoryApi.listToner(asset.value.id, { pageSize: 10 })
    history.value = data.items
  } catch (err: any) {
    console.error('TonerDialog.loadHistory failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el historial de tóner.')
  } finally {
    loadingHistory.value = false
  }
}

function open(target: { id: string; label: string }) {
  asset.value = target
  form.itemId = ''
  form.quantity = 1
  form.deliveredToUser = false
  form.occurredAt = new Date()
  form.counterValue = undefined
  form.notes = ''
  history.value = []
  visible.value = true
  void searchToner('')
  void loadHistory()
}

async function save() {
  if (!asset.value || !form.itemId || form.counterValue == null) return
  saving.value = true
  try {
    const { data } = await inventoryApi.registerToner({
      assetId: asset.value.id,
      itemId: form.itemId,
      quantity: form.quantity,
      occurredAt: (form.occurredAt ?? new Date()).toISOString(),
      deliveredToUser: form.deliveredToUser,
      counterValue: form.counterValue,
      notes: form.notes.trim() || null
    })
    ElMessage.success('Tóner registrado.')
    if (data.stockWarning) ElMessage({ message: data.stockWarning, type: 'warning', duration: 6000 })
    form.itemId = ''
    form.quantity = 1
    form.occurredAt = new Date()
    form.counterValue = undefined
    form.notes = ''
    await loadHistory()
    emit('saved')
  } catch (err: any) {
    console.error('TonerDialog.save failed', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo registrar el tóner.')
  } finally {
    saving.value = false
  }
}

const dateFormatter = new Intl.DateTimeFormat('es-CO', { dateStyle: 'medium', timeStyle: 'short' })
const disableFuture = (d: Date) => d.getTime() > Date.now() + 5 * 60 * 1000

defineExpose({ open })
</script>

<template>
  <el-dialog v-model="visible" :title="`Tóner — ${asset?.label ?? ''}`" width="620px">
    <el-form label-position="top">
      <el-form-item label="Tóner">
        <el-select v-model="form.itemId" filterable remote :remote-method="searchToner" :loading="searching" placeholder="Busca el tóner" style="width: 100%">
          <el-option v-for="o in options" :key="o.itemId" :label="o.name" :value="o.itemId">
            <span>{{ o.name }}</span>
            <span class="option-stock" :class="{ low: o.stock <= 0 }">stock {{ o.stock }}</span>
          </el-option>
        </el-select>
      </el-form-item>
      <div class="grid">
        <el-form-item label="Cantidad"><el-input-number v-model="form.quantity" :min="1" :max="100" style="width: 100%" /></el-form-item>
        <el-form-item label="Fecha y hora (por defecto, ahora)">
          <el-date-picker v-model="form.occurredAt" type="datetime" :clearable="false" :disabled-date="disableFuture" format="DD/MM/YYYY HH:mm" style="width: 100%" />
        </el-form-item>
      </div>
      <el-form-item label="¿Cómo se entregó?">
        <el-radio-group v-model="form.deliveredToUser">
          <el-radio :value="false">Lo cambié yo en la máquina</el-radio>
          <el-radio :value="true">Se lo dejé al usuario para que lo cambie</el-radio>
        </el-radio-group>
      </el-form-item>
      <div class="grid">
        <el-form-item label="Contador de la máquina (obligatorio)"><el-input-number v-model="form.counterValue" :min="0" :controls="false" style="width: 100%" /></el-form-item>
        <el-form-item label="Notas (opcional)"><el-input v-model="form.notes" maxlength="500" /></el-form-item>
      </div>
      <el-button type="primary" :disabled="!form.itemId || form.counterValue == null" :loading="saving" @click="save">Registrar</el-button>
    </el-form>

    <h4>Últimos registros de esta máquina</h4>
    <el-table v-loading="loadingHistory" :data="history" size="small" empty-text="Todavía no hay tóner registrado.">
      <el-table-column label="Fecha" width="170"><template #default="{ row }">{{ dateFormatter.format(new Date(row.occurredAt)) }}</template></el-table-column>
      <el-table-column prop="itemName" label="Tóner" min-width="160" />
      <el-table-column prop="quantity" label="Cant." width="70" />
      <el-table-column prop="counterValue" label="Contador" width="100" />
      <el-table-column prop="registeredBy" label="Registró" width="140" />
    </el-table>
  </el-dialog>
</template>

<style scoped>
.grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0 1rem; }
h4 { margin: 1.25rem 0 0.5rem; }
.option-stock { float: right; font-size: 0.75rem; color: var(--el-text-color-secondary); }
.option-stock.low { color: var(--el-color-danger); }
</style>
