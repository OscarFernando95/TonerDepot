import type { AxiosResponse } from 'axios'
import { http } from './http'

// Envelope que devuelven los endpoints paginados del backend (CODE_QUALITY_AUDIT.md hallazgo #4).
// TotalCount/Page solo vienen en modo offset; NextCursor solo en modo cursor (keyset).
export interface PagedResult<T> {
  items: T[]
  pageSize: number
  hasMore: boolean
  totalCount?: number | null
  page?: number | null
  nextCursor?: string | null
}

// Paso 2 del plan: la capa api desenvuelve .items y sigue devolviendo el array plano, así que las
// 19 vistas no se tocan y siguen funcionando igual. El Paso 3 (vista por vista) irá reemplazando
// estas llamadas por getPaged() a medida que cada vista reciba UI de paginación real.
//
// El aviso en dev es la red de seguridad contra el único modo de fallo de este enfoque: el
// TRUNCAMIENTO SILENCIOSO. Con los volúmenes actuales no se trunca nada; el día que una tabla pase
// del tamaño de página, la vista mostraría una lista incompleta sin ninguna señal. Esto la da.
export async function getList<T>(url: string): Promise<AxiosResponse<T[]>> {
  const response = await http.get<PagedResult<T>>(url)

  if (import.meta.env.DEV && response.data.hasMore) {
    console.warn(
      `[paginación] ${url} devolvió una página incompleta (pageSize=${response.data.pageSize}` +
        `${response.data.totalCount != null ? `, total=${response.data.totalCount}` : ''}). ` +
        'Esta vista todavía no pagina, así que está mostrando datos parciales — ' +
        'toca migrarla a getPaged() (Paso 3 del hallazgo #4).'
    )
  }

  return { ...response, data: response.data.items }
}

// Para el Paso 3: devuelve el envelope completo, con total/página o cursor según el endpoint.
export function getPaged<T>(
  url: string,
  params?: { page?: number; pageSize?: number; cursor?: string }
): Promise<AxiosResponse<PagedResult<T>>> {
  return http.get<PagedResult<T>>(url, { params })
}
