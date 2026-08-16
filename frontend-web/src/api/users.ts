import { http } from './http'
import type { CreateUserRequest, UpdateUserRequest, UserDto } from './types'

export function listUsers() {
  return http.get<UserDto[]>('/users')
}

export function createUser(request: CreateUserRequest) {
  return http.post<UserDto>('/users', request)
}

export function updateUser(id: string, request: UpdateUserRequest) {
  return http.patch<UserDto>(`/users/${id}`, request)
}

export function setUserStatus(id: string, isActive: boolean) {
  return http.patch<UserDto>(`/users/${id}/status`, { isActive })
}

export function resetUserPassword(id: string) {
  return http.post<UserDto>(`/users/${id}/reset-password`)
}
