<script setup lang="ts">
import MaintenanceOrderThread from './MaintenanceOrderThread.vue'

// El hilo de una orden dentro de un diálogo, para las listas (portal del cliente y trabajo del técnico).
defineProps<{
  modelValue: boolean
  orderId: string | null
  orderStatus: string
  title?: string
  helpText?: string
  readOnly?: boolean
}>()

const emit = defineEmits<{ 'update:modelValue': [value: boolean]; sent: [] }>()
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="title ?? 'Comentarios'"
    width="min(560px, 94vw)"
    destroy-on-close
    @update:model-value="emit('update:modelValue', $event)"
  >
    <MaintenanceOrderThread
      v-if="orderId"
      :order-id="orderId"
      :order-status="orderStatus"
      :help-text="helpText"
      :read-only="readOnly"
      @sent="emit('sent')"
    />
  </el-dialog>
</template>
