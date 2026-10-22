<script setup lang="ts">
import type { ClientLocationAccess } from '../composables/useClientLocationAccess'

// Control "Acceso a sedes" de un usuario Cliente. Presentacional: el estado vive en
// useClientLocationAccess (lo comparten el alta de usuario y el diálogo "Sedes de acceso").
defineProps<{ access: ClientLocationAccess; showError?: boolean }>()
</script>

<template>
  <el-form-item label="Acceso a sedes" :error="showError ? access.validationError ?? '' : ''">
    <el-radio-group v-model="access.mode.value" class="access-modes">
      <el-radio value="all">Todas las sedes de la empresa</el-radio>
      <el-radio value="some">Solo algunas sedes</el-radio>
    </el-radio-group>
    <el-select
      v-if="access.mode.value === 'some'"
      v-model="access.selectedIds.value"
      class="access-select"
      multiple
      filterable
      collapse-tags
      collapse-tags-tooltip
      :loading="access.loading.value"
      placeholder="Selecciona las sedes"
      no-data-text="Esta empresa no tiene sedes activas."
    >
      <el-option
        v-for="l in access.options.value"
        :key="l.id"
        :label="l.isActive ? l.name : `${l.name} (inactiva)`"
        :value="l.id"
      />
    </el-select>
    <p v-if="access.loadFailed.value" class="access-hint access-hint-error">No se pudieron cargar las sedes de la empresa.</p>
    <p v-else-if="access.mode.value === 'all'" class="access-hint">Podrá ver y reportar tickets de cualquier sede de su empresa.</p>
  </el-form-item>
</template>

<style scoped>
.access-modes {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
}

.access-select {
  width: 100%;
  margin-top: 0.25rem;
}

.access-hint {
  margin: 0.25rem 0 0;
  font-size: 0.8rem;
  color: var(--el-text-color-secondary);
}

.access-hint-error {
  color: var(--el-color-danger);
}
</style>
