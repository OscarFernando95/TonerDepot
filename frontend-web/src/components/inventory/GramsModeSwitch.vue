<script setup lang="ts">
import { gramsMode } from '../../composables/useGramsDisplay'
</script>

<template>
  <span class="grams-switch" title="Cómo se muestra el saldo del revelador (se guarda siempre en gramos)">
    <span class="grams-switch-label">Revelador</span>
    <el-radio-group v-model="gramsMode" size="small">
      <el-radio-button value="g">Gramos</el-radio-button>
      <el-radio-button value="kg">Kg y bolsas</el-radio-button>
    </el-radio-group>
  </span>
</template>

<style scoped>
.grams-switch {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}
.grams-switch-label {
  font-size: 0.8rem;
  color: var(--el-text-color-secondary);
}
</style>
