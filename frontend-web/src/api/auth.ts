import { http } from './http'
import type { CurrentUser, LoginResult } from './types'

export function login(cedula: string, password: string) {
  return http.post<LoginResult>('/auth/login', { cedula, password })
}

export function me() {
  return http.get<CurrentUser>('/auth/me')
}

export function changePassword(currentPassword: string, newPassword: string) {
  return http.post('/auth/change-password', { currentPassword, newPassword })
}
