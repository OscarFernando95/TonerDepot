import { http } from './http'
import type { ClientHomeDto } from './types'

// Inicio del Cliente: tickets, mantenimientos, instalaciones y estado de sus máquinas, de un solo llamado.
// Solo rol Cliente; el servidor lo acota a la empresa del usuario.
export function getClientHome() {
  return http.get<ClientHomeDto>('/client-portal/home')
}
