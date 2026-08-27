<script setup lang="ts">
import { computed, reactive, ref, toRef } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Check, Close } from '@element-plus/icons-vue'
import * as authApi from '../../api/auth'
import { useAuthStore } from '../../stores/auth'
import { isPasswordValid, usePasswordRuleStatus } from '../../composables/usePasswordRules'

const auth = useAuthStore()
const router = useRouter()

const form = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })
const saving = ref(false)

const ruleStatus = usePasswordRuleStatus(toRef(form, 'newPassword'))
const canSubmit = computed(
  () => isPasswordValid(form.newPassword) && form.newPassword === form.confirmPassword && form.currentPassword !== ''
)

async function handleSubmit() {
  if (!canSubmit.value) {
    return
  }
  if (form.newPassword !== form.confirmPassword) {
    ElMessage.error('La nueva contraseña y su confirmación no coinciden.')
    return
  }

  saving.value = true
  try {
    await authApi.changePassword(form.currentPassword, form.newPassword)
    auth.setMustChangePassword(false)
    ElMessage.success('Contraseña actualizada.')
    router.push({ name: 'dashboard' })
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cambiar la contraseña.')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="change-password-page">
    <el-card class="change-password-card">
      <h1>Cambiar contraseña</h1>
      <p class="hint">
        Tu cuenta tiene una contraseña genérica. Debes cambiarla antes de continuar usando el sistema.
      </p>

      <el-form :model="form" label-position="top" @submit.prevent="handleSubmit">
        <el-form-item label="Contraseña actual">
          <el-input v-model="form.currentPassword" type="password" show-password autofocus />
        </el-form-item>
        <el-form-item label="Nueva contraseña">
          <el-input v-model="form.newPassword" type="password" show-password />
        </el-form-item>

        <ul class="password-rules">
          <li
            v-for="rule in ruleStatus"
            :key="rule.key"
            :class="['password-rule', rule.met ? 'password-rule--met' : 'password-rule--pending']"
          >
            <el-icon><Check v-if="rule.met" /><Close v-else /></el-icon>
            {{ rule.label }}
          </li>
        </ul>

        <el-form-item label="Confirmar nueva contraseña">
          <el-input v-model="form.confirmPassword" type="password" show-password @keyup.enter="handleSubmit" />
        </el-form-item>
        <el-button
          type="primary"
          class="submit-button"
          :loading="saving"
          :disabled="!canSubmit"
          @click="handleSubmit"
        >
          Cambiar contraseña
        </el-button>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped>
.change-password-page {
  min-height: calc(100vh - 60px);
  display: flex;
  align-items: center;
  justify-content: center;
}

.change-password-card {
  width: 400px;
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: var(--text-md);
  margin: 0.25rem 0 1.5rem;
}

.submit-button {
  width: 100%;
}

.password-rules {
  list-style: none;
  margin: -0.5rem 0 1rem;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.password-rule {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: var(--text-sm);
}

.password-rule--met {
  color: var(--el-color-success);
}

.password-rule--pending {
  color: var(--el-text-color-secondary);
}
</style>
