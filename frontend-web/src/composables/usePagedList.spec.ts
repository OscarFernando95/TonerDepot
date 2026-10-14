import { afterEach, describe, expect, it, vi } from 'vitest'
import { effectScope, nextTick, reactive } from 'vue'
import type { PagedResult } from '../api/paging'
import { usePagedList } from './usePagedList'

// AUDIT.md H-11 (Paso 3): lógica común de las vistas paginadas por el servidor.
const page = <T,>(items: T[], extra: Partial<PagedResult<T>> = {}): PagedResult<T> => ({
  items, pageSize: 25, hasMore: false, totalCount: items.length, page: 1, ...extra
})

// Promesa controlable, para simular respuestas que llegan en otro orden.
function deferred<T>() {
  let resolve!: (v: T) => void
  const promise = new Promise<T>((r) => { resolve = r })
  return { promise, resolve }
}

const scopes: ReturnType<typeof effectScope>[] = []
function setup<T, F extends object>(...args: Parameters<typeof usePagedList<T, F>>) {
  const scope = effectScope()
  scopes.push(scope)
  return scope.run(() => usePagedList<T, F>(...args))!
}
afterEach(() => { scopes.splice(0).forEach((s) => s.stop()); vi.restoreAllMocks() })

describe('usePagedList', () => {
  it('reload() pide la página 1 con los filtros y llena items y total', async () => {
    const fetcher = vi.fn().mockResolvedValue(page(['a', 'b'], { totalCount: 57 }))
    const list = setup(fetcher, { filters: reactive({ status: 'Activo' }), pageSize: 25 })

    await list.reload()

    expect(fetcher).toHaveBeenCalledWith({ status: 'Activo', page: 1, pageSize: 25 })
    expect(list.items.value).toEqual(['a', 'b'])
    expect(list.total.value).toBe(57)
    expect(list.loading.value).toBe(false)
  })

  it('al cambiar un filtro vuelve a la página 1 y pide con el filtro nuevo', async () => {
    const fetcher = vi.fn().mockResolvedValue(page(['x']))
    const filters = reactive({ status: '' })
    const list = setup(fetcher, { filters, pageSize: 10 })
    await list.reload()
    list.page.value = 3
    await nextTick(); await Promise.resolve()
    fetcher.mockClear()

    filters.status = 'Cerrado'
    await nextTick(); await Promise.resolve()

    expect(list.page.value).toBe(1)
    expect(fetcher).toHaveBeenCalledTimes(1)
    expect(fetcher).toHaveBeenLastCalledWith({ status: 'Cerrado', page: 1, pageSize: 10 })
  })

  it('al cambiar de página conserva los filtros', async () => {
    const fetcher = vi.fn().mockResolvedValue(page(['x']))
    const list = setup(fetcher, { filters: reactive({ clientId: 'c1' }), pageSize: 10 })
    await list.reload()
    fetcher.mockClear()

    list.page.value = 2
    await nextTick(); await Promise.resolve()

    expect(fetcher).toHaveBeenCalledWith({ clientId: 'c1', page: 2, pageSize: 10 })
  })

  it('descarta una respuesta vieja que llega después de una más nueva', async () => {
    const slow = deferred<PagedResult<string>>()
    const fast = deferred<PagedResult<string>>()
    const fetcher = vi.fn().mockReturnValueOnce(slow.promise).mockReturnValueOnce(fast.promise)
    const list = setup(fetcher, { filters: reactive({}), pageSize: 10 })

    const first = list.reload()   // pedido viejo (lento)
    const second = list.reload()  // pedido nuevo (rápido)
    fast.resolve(page(['nuevo']))
    await second
    slow.resolve(page(['viejo']))
    await first

    expect(list.items.value).toEqual(['nuevo'])
  })

  it('un error se registra con console.error, avisa y deja de cargar sin vaciar lo anterior', async () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {})
    const onError = vi.fn()
    const fetcher = vi.fn().mockResolvedValueOnce(page(['previo'])).mockRejectedValueOnce(new Error('boom'))
    const list = setup(fetcher, { filters: reactive({}), pageSize: 10, label: 'Tickets', onError })
    await list.reload()

    await list.reload()

    expect(error).toHaveBeenCalledWith('Tickets: no se pudo cargar', expect.any(Error))
    expect(onError).toHaveBeenCalledTimes(1)
    expect(list.items.value).toEqual(['previo'])
    expect(list.loading.value).toBe(false)
  })

  it('sin totalCount usa la cantidad de filas', async () => {
    const fetcher = vi.fn().mockResolvedValue(page(['a', 'b', 'c'], { totalCount: null }))
    const list = setup(fetcher, { filters: reactive({}), pageSize: 10 })

    await list.reload()

    expect(list.total.value).toBe(3)
  })
})
