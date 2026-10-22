<script setup lang="ts">
import { DEFAULT_WARNING_PRINTS } from '../../composables/useRentalRetirement'

// Campos de "salida de alquiler" del formulario de un modelo (alta rápida y alta desde la marca).
const threshold = defineModel<number | null>('threshold', { required: true })
const warningPrints = defineModel<number>('warningPrints', { required: true })
</script>

<template>
  <el-form-item label="Impresiones para salir de alquiler (opcional)">
    <el-input-number
      v-model="threshold"
      :min="1"
      :precision="0"
      :value-on-clear="null"
      placeholder="Sin límite"
      style="width: 100%"
    />
    <div class="field-help">Contador total del equipo; es informativo. Vacío = sin límite.</div>
  </el-form-item>
  <el-form-item label="Avisar cuando falten (impresiones)">
    <el-input-number
      v-model="warningPrints"
      :min="1"
      :precision="0"
      :value-on-clear="DEFAULT_WARNING_PRINTS"
      style="width: 100%"
    />
    <div class="field-help">Por defecto 50.000.</div>
  </el-form-item>
</template>

<style scoped>
.field-help {
  width: 100%;
  font-size: 0.75rem;
  line-height: 1.3;
  color: var(--el-text-color-secondary);
  margin-top: 0.25rem;
}
</style>
