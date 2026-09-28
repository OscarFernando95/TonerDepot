import axios from 'axios'
import router from '../router'
import { useAuthStore } from '../stores/auth'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
  timeout: 15000
})

http.interceptors.request.use((config) => {
  // Informativo para la auditoría y el mensaje de sesión única (nunca una decisión de seguridad).
  config.headers['X-Client-Type'] = 'web'
  const auth = useAuthStore()
  if (auth.token) {
    config.headers.Authorization = `Bearer ${auth.token}`
  }
  return config
})

http.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const auth = useAuthStore()
      auth.logout()
      router.push({ name: 'login' })
    }
    return Promise.reject(error)
  }
)
