import { onMounted, ref, watch } from 'vue'
import type { PagedResult } from '../api/paging'

// Lista paginada por el SERVIDOR (AUDIT.md H-11, Paso 3). Los filtros viajan al servidor y la tabla muestra solo la
// página pedida; antes cada vista traía la primera página (200 filas), filtraba en el cliente y se truncaba en
// silencio al superarla.
//
// - Al cambiar un filtro vuelve a la página 1 y pide de nuevo; al cambiar de página conserva los filtros.
// - Descarta respuestas obsoletas: si el filtro cambia mientras llega la anterior, gana siempre la última petición.
// - Un error se registra con console.error (convención del proyecto) y se avisa por onError; la lista anterior queda.
export interface PagedListOptions<F extends object> {
  filters: F
  pageSize?: number
  // Nombre para el log de errores.
  label?: string
  onError?: (err: unknown) => void
  // Pedir al montar (por defecto sí).
  immediate?: boolean
}

export function usePagedList<T, F extends object>(
  fetcher: (params: F & { page: number; pageSize: number }) => Promise<PagedResult<T>>,
  options: PagedListOptions<F>
) {
  const pageSize = options.pageSize ?? 25
  const items = ref<T[]>([])
  const total = ref(0)
  const page = ref(1)
  const loading = ref(false)

  let latest = 0

  async function reload(): Promise<void> {
    const request = ++latest
    loading.value = true
    try {
      const result = await fetcher({ ...options.filters, page: page.value, pageSize })
      if (request !== latest) return // llegó una respuesta más nueva: esta ya no vale
      items.value = result.items
      total.value = result.totalCount ?? result.items.length
    } catch (err) {
      if (request !== latest) return
      console.error(`${options.label ?? 'Lista'}: no se pudo cargar`, err)
      options.onError?.(err)
    } finally {
      if (request === latest) loading.value = false
    }
  }

  // Un filtro nuevo siempre empieza en la página 1. Si ya estaba en la 1, la petición la dispara este watch; si no,
  // la dispara el cambio de página que provoca (un solo pedido en cualquiera de los dos casos).
  watch(
    () => ({ ...options.filters }),
    () => {
      if (page.value !== 1) {
        page.value = 1
      } else {
        void reload()
      }
    },
    { deep: true }
  )
  watch(page, () => void reload())

  if (options.immediate !== false) {
    onMounted(() => void reload())
  }

  return { items, total, page, pageSize, loading, reload }
}
