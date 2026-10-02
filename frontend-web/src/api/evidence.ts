import { http } from './http'

export interface EvidenceDto {
  id: string
  kind: 'Antes' | 'Despues' | 'Contador'
  contentType: string
  sizeBytes: number
  uploadedAt: string
  serviceTicketId: string | null
  maintenanceOrderId: string | null
  timeLogId: string | null
}

export function listEvidence(target: { ticketId?: string; orderId?: string }) {
  return http.get<EvidenceDto[]>('/evidence', { params: target })
}

// El contenido va detrás de autenticación (contenedor privado, sin URLs firmadas): se baja como blob con
// el token y se muestra con un object URL.
export function getEvidenceContent(id: string) {
  return http.get<Blob>(`/evidence/${id}/content`, { responseType: 'blob' })
}
