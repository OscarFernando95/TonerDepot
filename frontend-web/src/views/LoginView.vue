<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '../stores/auth'
import FlapText from '../components/board/FlapText.vue'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const form = reactive({ cedula: '', password: '' })
const loading = ref(false)
const loginError = ref('')

// Solo una credencial mala debe decir "incorrectos": un servidor caído o un límite de intentos no tienen nada que ver
// con la contraseña, y mostrarlos así manda al usuario a sospechar de su cuenta.
function loginErrorMessage(err: any): string {
  const status: number | undefined = err?.response?.status
  if (!err?.response) return 'No se pudo conectar con el servidor. Revisa tu conexión o inténtalo en un momento.'
  if (status === 409) return err.response.data?.title ?? 'Ya tienes una sesión activa en otro dispositivo.'
  if (status === 429) return 'Demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.'
  if (status === 401 || status === 400) return 'Cédula o contraseña incorrectos.'
  return 'Ocurrió un error inesperado. Inténtalo de nuevo; si continúa, avisa al administrador.'
}

async function handleSubmit() {
  loading.value = true
  loginError.value = ''
  try {
    await auth.login(form.cedula, form.password)
    const redirect = (route.query.redirect as string) || '/dashboard'
    router.push(redirect)
  } catch (err: any) {
    console.error('LoginView.handleSubmit failed', err)
    loginError.value = loginErrorMessage(err)
    ElMessage.error(loginError.value)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="gate-scene">
    <div class="gate-card">
      <div class="gate-wordmark">
        <FlapText value="TONER" />
      </div>
      <p class="gate-subtitle">Gestión de alquiler y servicio técnico de impresoras</p>

      <el-form :model="form" label-position="top" class="gate-form" @submit.prevent="handleSubmit">
        <el-form-item label="Cédula">
          <el-input v-model="form.cedula" placeholder="Número de cédula" autofocus />
        </el-form-item>
        <el-form-item label="Contraseña">
          <el-input v-model="form.password" type="password" show-password @keyup.enter="handleSubmit" />
        </el-form-item>

        <p v-if="loginError" class="gate-error">
          <span class="gate-error-lamp" aria-hidden="true"></span>
          {{ loginError }}
        </p>

        <el-button type="primary" class="gate-submit" :loading="loading" @click="handleSubmit">
          Ingresar
        </el-button>
      </el-form>
    </div>
  </div>
</template>

<style scoped>
.gate-scene {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background:
    repeating-linear-gradient(
      90deg,
      var(--board-seam-soft) 0,
      var(--board-seam-soft) 1px,
      transparent 1px,
      transparent 96px
    ),
    var(--board-bg);
}

.gate-card {
  width: 380px;
  padding: 2rem 2rem 2.25rem;
  background: var(--board-panel);
  border: 1px solid var(--board-seam-soft);
  box-shadow: 0 24px 48px rgba(0, 0, 0, 0.45);
}

.gate-wordmark {
  font-family: var(--font-display);
  font-weight: 700;
  font-size: var(--text-display-lg);
  letter-spacing: 0.08em;
  color: var(--flap-ink);
  text-align: center;
}

.gate-subtitle {
  margin: 0.35rem 0 1.75rem;
  text-align: center;
  color: var(--flap-ink-dim);
  font-size: var(--text-md);
}

.gate-error {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  margin: -0.25rem 0 1rem;
  font-size: var(--text-sm);
  color: var(--signal-red);
}

.gate-error-lamp {
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  border: 2px solid var(--signal-red);
  flex: none;
}

.gate-submit {
  width: 100%;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-weight: 600;
}
</style>
