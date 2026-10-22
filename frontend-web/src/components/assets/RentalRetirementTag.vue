<script setup lang="ts">
import { computed } from 'vue'
import type { RentalRetirementSubject } from '../../composables/useRentalRetirement'
import { rentalRetirementDisplay } from '../../composables/useRentalRetirement'

// Aviso informativo de retiro de alquiler (ámbar = próxima, rojo = superó el límite).
// No renderiza nada si el equipo no tiene estado (sin límite, sin lecturas o rol Cliente).
const props = defineProps<{ subject: RentalRetirementSubject }>()
const display = computed(() => rentalRetirementDisplay(props.subject))
</script>

<template>
  <div v-if="display" class="rental-retirement" :data-status="display.status">
    <el-tag :type="display.tagType" size="small" :effect="display.status === 'Superada' ? 'dark' : 'light'">
      {{ display.label }}
    </el-tag>
    <span v-if="display.detail" class="rental-retirement-detail">{{ display.detail }}</span>
  </div>
</template>

<style scoped>
.rental-retirement {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.15rem;
  margin-top: 0.25rem;
}

.rental-retirement-detail {
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
}
</style>
