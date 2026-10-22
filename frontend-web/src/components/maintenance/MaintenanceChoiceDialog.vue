<script setup lang="ts">
import { watch } from 'vue'
import { useMaintenanceChoice, type MaintenanceChoice } from '../../composables/useMaintenanceChoice'

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
  }>(),
  { subtitle: undefined, hint: undefined, initialGeneral: false, loading: false }
)

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: [choice: MaintenanceChoice]
}>()

const { form, valid, reset, toChoice } = useMaintenanceChoice(props.initialGeneral)

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
      <el-form-item label="Motivo (opcional)">
        <el-input v-model="form.reason" type="textarea" :rows="2" maxlength="500" show-word-limit placeholder="Ej: falla de arrastre reportada por el cliente" />
      </el-form-item>
      <p v-if="hint" class="hint">{{ hint }}</p>
    </el-form>
    <template #footer>
      <el-button @click="emit('update:modelValue', false)">Cancelar</el-button>
      <el-button type="primary" :disabled="!valid" :loading="loading" @click="emit('confirm', toChoice())">{{ confirmLabel }}</el-button>
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

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}
</style>
