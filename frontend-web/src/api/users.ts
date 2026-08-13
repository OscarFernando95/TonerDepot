import { http } from './http'
import type { CreateUserRequest, UserDto } from './types'

export function listUsers() {
  return http.get<UserDto[]>('/users')
}

export function createUser(request: CreateUserRequest) {
  return http.post<UserDto>('/users', request)
}

export function setUserStatus(id: string, isActive: boolean) {
  return http.patch<UserDto>(`/users/${id}/status`, { isActive })
}
