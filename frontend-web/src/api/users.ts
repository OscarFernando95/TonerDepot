import { getList } from './paging'
import { http } from './http'
import type { CreateUserRequest, UpdateUserRequest, UserDto, UserWithGeneratedPasswordDto } from './types'

export function listUsers() {
  return getList<UserDto>('/users')
}

export function createUser(request: CreateUserRequest) {
  return http.post<UserWithGeneratedPasswordDto>('/users', request)
}

export function updateUser(id: string, request: UpdateUserRequest) {
  return http.patch<UserDto>(`/users/${id}`, request)
}

export function setUserStatus(id: string, isActive: boolean) {
  return http.patch<UserDto>(`/users/${id}/status`, { isActive })
}

export function resetUserPassword(id: string) {
  return http.post<UserWithGeneratedPasswordDto>(`/users/${id}/reset-password`)
}
