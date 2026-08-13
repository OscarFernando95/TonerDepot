<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const form = reactive({ cedula: '', password: '' })
const loading = ref(false)

async function handleSubmit() {
  loading.value = true
  try {
    await auth.login(form.cedula, form.password)
    const redirect = (route.query.redirect as string) || '/dashboard'
    router.push(redirect)
  } catch {
    ElMessage.error('Cédula o contraseña incorrectos.')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <el-card class="login-card">
      <h1 class="login-title">Toner</h1>
      <p class="login-subtitle">Gestión de alquiler y servicio técnico de impresoras</p>

      <el-form :model="form" label-position="top" @submit.prevent="handleSubmit">
        <el-form-item label="Cédula">
          <el-input v-model="form.cedula" placeholder="Número de cédula" autofocus />
        </el-form-item>
        <el-form-item label="Contraseña">
          <el-input v-model="form.password" type="password" show-password @keyup.enter="handleSubmit" />
        </el-form-item>
        <el-button type="primary" class="login-button" :loading="loading" @click="handleSubmit">
          Ingresar
        </el-button>
      </el-form>
    </el-card>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1f2937, #374151);
}

.login-card {
  width: 360px;
}

.login-title {
  margin: 0;
  font-size: 1.6rem;
  text-align: center;
}

.login-subtitle {
  margin: 0.25rem 0 1.5rem;
  text-align: center;
  color: #6b7280;
  font-size: 0.85rem;
}

.login-button {
  width: 100%;
}
</style>
