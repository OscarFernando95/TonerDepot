<script setup lang="ts">
import { computed, watch } from 'vue'
import { confirmButtonLabel, useMaintenanceChoice, type MaintenanceChoice } from '../../composables/useMaintenanceChoice'
import {
  disabledPlannedHours,
  disabledPlannedMinutes,
  isPlannedDateDisabled
} from '../../composables/useMaintenanceSchedule'

// Diálogo para elegir el tipo de mantenimiento a demanda. Lo usan el staff (AssetDetailView, "Pedir") y el técnico
// (MeterReadingsView, "Iniciar"). Quien lo abre hace la llamada: este componente solo recoge la elección.
const props = withDefaults(
  defineProps<{
    modelValue: boolean
    title: string
    confirmLabel: string
    subtitle?: string
    hint?: string
    initialGeneral?: boolean
    loading?: boolean
    // Solo el técnico: ofrece "¿Cuándo lo harás?" (ahora o programarlo para otra fecha). El staff no lo ve.
    allowSchedule?: boolean
  }>(),
  { subtitle: undefined, hint: undefined, initialGeneral: false, loading: false, allowSchedule: false }
)

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: [choice: MaintenanceChoice]
}>()

const { form, valid, scheduling, plannedError, reset, toChoice } = useMaintenanceChoice(props.initialGeneral)
const buttonLabel = computed(() => confirmButtonLabel(props.confirmLabel, props.allowSchedule && scheduling.value))

// Cada vez que se abre arranca limpio: no arrastra la elección de la vez anterior.
watch(
  () => props.modelValue,
  (open) => {
    if (open) reset()
  }
)
</script>

<template>
  <el-dialog :model-value="modelValue" :title="title" width="440px" @update:model-value="emit('update:modelValue', $event)">
    <p v-if="subtitle" class="subtitle">{{ subtitle }}</p>
    <el-form label-position="top">
      <el-form-item label="Qué se hará en la visita">
        <div class="checks">
          <el-checkbox v-model="form.includesGeneral">Mantenimiento general</el-checkbox>
          <el-checkbox v-model="form.includesUnits">Mantenimiento de unidades</el-checkbox>
          <el-checkbox v-model="form.includesConsumables">Cambio de insumos</el-checkbox>
        </div>
      </el-form-item>
      <el-form-item v-if="allowSchedule" label="¿Cuándo lo harás?">
        <el-radio-group v-model="form.when" class="when-options">
          <el-radio value="now">Ahora</el-radio>
          <el-radio value="later">Programarlo para otra fecha</el-radio>
        </el-radio-group>
        <el-date-picker
          v-if="scheduling"
          v-model="form.plannedAt"
          type="datetime"
          class="when-picker"
          placeholder="Elige fecha y hora (tu hora local)"
          format="DD/MM/YYYY HH:mm"
          :clearable="false"
          :disabled-date="(day: Date) => isPlannedDateDisabled(day)"
          :disabled-hours="() => disabledPlannedHours(form.plannedAt)"
          :disabled-minutes="(hour: number) => disabledPlannedMinutes(hour, form.plannedAt)"
        />
        <p v-if="scheduling && plannedError" class="when-error" role="alert">{{ plannedError }}</p>
        <p v-else-if="scheduling" class="when-hint">El cliente verá esta fecha y podrá avisarte si no le sirve.</p>
      </el-form-item>
      <el-form-item label="Motivo (opcional)">
        <el-input v-model="form.reason" type="textarea" :rows="2" maxlength="500" show-word-limit placeholder="Ej: falla de arrastre reportada por el cliente" />
      </el-form-item>
      <p v-if="hint" class="hint">{{ hint }}</p>
    </el-form>
    <template #footer>
      <el-button @click="emit('update:modelValue', false)">Cancelar</el-button>
      <el-button type="primary" :disabled="!valid" :loading="loading" @click="emit('confirm', toChoice())">{{ buttonLabel }}</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.subtitle {
  margin: 0 0 1rem;
  font-weight: 500;
}

.checks {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.when-options {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
}

.when-picker {
  width: 100%;
  margin-top: 0.5rem;
}

.when-error,
.when-hint {
  width: 100%;
  margin: 0.4rem 0 0;
  font-size: var(--text-md);
  line-height: 1.4;
}

.when-error {
  color: var(--signal-red);
}

.when-hint {
  color: var(--el-text-color-secondary);
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}
</style>
