import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import * as authApi from '../api/auth'
import type { CurrentUser } from '../api/types'

const TOKEN_KEY = 'toner_token'
const USER_KEY = 'toner_user'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const user = ref<CurrentUser | null>(readStoredUser())

  const isAuthenticated = computed(() => !!token.value)
  const role = computed(() => user.value?.role ?? null)

  function readStoredUser(): CurrentUser | null {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as CurrentUser) : null
  }

  async function login(email: string, password: string) {
    const { data } = await authApi.login(email, password)
    if (!data.succeeded || !data.token || !data.user) {
      throw new Error('Credenciales inválidas')
    }
    token.value = data.token
    user.value = data.user
    localStorage.setItem(TOKEN_KEY, data.token)
    localStorage.setItem(USER_KEY, JSON.stringify(data.user))
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(USER_KEY)
  }

  function hasRole(...roles: string[]) {
    return !!user.value && roles.includes(user.value.role)
  }

  return { token, user, isAuthenticated, role, login, logout, hasRole }
})
