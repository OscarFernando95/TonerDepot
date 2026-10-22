<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as selfApi from '../../api/technicianSelf'
import type { MaintenanceOrderDto } from '../../api/types'
import {
  disabledPlannedHours,
  disabledPlannedMinutes,
  formatPlannedAt,
  isPlannedDateDisabled,
  toPlannedIso,
  validatePlannedAt
} from '../../composables/useMaintenanceSchedule'
import { markThreadSeen } from '../../composables/useMaintenanceThread'
import { apiErrorMessage } from '../../utils/apiError'

// El técnico asignado fija (o cambia) cuándo hará el mantenimiento. El picker trabaja en hora local y se envía en ISO UTC.
const props = defineProps<{ modelValue: boolean; order: MaintenanceOrderDto | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean]; scheduled: [order: MaintenanceOrderDto] }>()

const picked = ref<Date | null>(null)
const saving = ref(false)
const serverError = ref<string | null>(null)

const reprogramming = computed(() => !!props.order?.plannedAt)
const error = computed(() => (picked.value ? validatePlannedAt(picked.value) : null))
const canSave = computed(() => !!picked.value && !error.value && !saving.value)

// Cada vez que se abre arranca con la fecha vigente (si hay) y sin errores de la vez anterior.
watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    serverError.value = null
    picked.value = props.order?.plannedAt ? new Date(props.order.plannedAt) : null
  }
)

async function save() {
  if (!props.order || !picked.value || error.value) return
  saving.value = true
  serverError.value = null
  try {
    const { data } = await selfApi.scheduleMaintenanceOrder(props.order.id, toPlannedIso(picked.value))
    // Su propio evento de programación no cuenta como "comentario nuevo" para él.
    markThreadSeen(data.id, data.lastCommentAt)
    ElMessage.success(`Mantenimiento programado para el ${formatPlannedAt(data.plannedAt)}.`)
    emit('update:modelValue', false)
    emit('scheduled', data)
  } catch (err: any) {
    console.error('No se pudo programar el mantenimiento:', err)
    serverError.value = apiErrorMessage(err, 'No se pudo programar el mantenimiento.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="reprogramming ? 'Reprogramar mantenimiento' : 'Programar mantenimiento'"
    width="min(420px, 94vw)"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <p v-if="order" class="subtitle">{{ order.assetBrandName }} {{ order.assetModel }} — {{ order.assetSerialNumber }}</p>
    <p v-if="order?.plannedAt" class="current">Programado actualmente: {{ formatPlannedAt(order.plannedAt) }}</p>
    <el-form label-position="top" @submit.prevent="save">
      <el-form-item label="Fecha y hora (tu hora local)">
        <el-date-picker
          v-model="picked"
          type="datetime"
          style="width: 100%"
          placeholder="Elige fecha y hora"
          format="DD/MM/YYYY HH:mm"
          :clearable="false"
          :disabled-date="(day: Date) => isPlannedDateDisabled(day)"
          :disabled-hours="() => disabledPlannedHours(picked)"
          :disabled-minutes="(hour: number) => disabledPlannedMinutes(hour, picked)"
        />
      </el-form-item>
      <p class="hint">El cliente verá esta fecha y podrá avisarte si no le sirve.</p>
      <p v-if="error || serverError" class="form-error" role="alert">{{ error ?? serverError }}</p>
    </el-form>
    <template #footer>
      <el-button class="tap-btn" @click="emit('update:modelValue', false)">Cancelar</el-button>
      <el-button type="primary" class="tap-btn" :loading="saving" :disabled="!canSave" @click="save">
        {{ reprogramming ? 'Reprogramar' : 'Programar' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.subtitle {
  margin: 0 0 0.25rem;
  color: var(--flap-ink);
  font-weight: 600;
}

.current,
.hint {
  margin: 0 0 0.75rem;
  color: var(--el-text-color-secondary);
  font-size: var(--text-md);
}

.form-error {
  margin: 0;
  color: var(--signal-red);
  font-size: var(--text-md);
}

.tap-btn {
  min-height: 44px;
  padding: 0 1.1rem;
}
</style>
