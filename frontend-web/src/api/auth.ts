import { http } from './http'
import type { CurrentUser, LoginResult } from './types'

export function login(email: string, password: string) {
  return http.post<LoginResult>('/auth/login', { email, password })
}

export function me() {
  return http.get<CurrentUser>('/auth/me')
}

export function changePassword(currentPassword: string, newPassword: string) {
  return http.post('/auth/change-password', { currentPassword, newPassword })
}
