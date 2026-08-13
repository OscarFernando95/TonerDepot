<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as authApi from '../../api/auth'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()
const router = useRouter()

const form = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })
const saving = ref(false)

async function handleSubmit() {
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
        <el-form-item label="Confirmar nueva contraseña">
          <el-input v-model="form.confirmPassword" type="password" show-password @keyup.enter="handleSubmit" />
        </el-form-item>
        <el-button type="primary" class="submit-button" :loading="saving" @click="handleSubmit">
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
  color: #6b7280;
  font-size: 0.85rem;
  margin: 0.25rem 0 1.5rem;
}

.submit-button {
  width: 100%;
}
</style>
