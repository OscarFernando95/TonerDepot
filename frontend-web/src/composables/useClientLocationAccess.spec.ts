import { beforeEach, describe, expect, it, vi } from 'vitest'

vi.mock('../api/clientLocations', () => ({ listClientLocations: vi.fn() }))

import { listClientLocations } from '../api/clientLocations'
import {
  LOCATION_ACCESS_REQUIRED_MESSAGE,
  locationAccessLabel,
  toAllowedLocationIds,
  useClientLocationAccess,
  validateLocationAccess
} from './useClientLocationAccess'

const loc = (id: string, isActive = true) => ({ id, name: `Sede ${id}`, isActive }) as any
const respond = (data: any[]) => ({ data }) as any

describe('locationAccessLabel', () => {
  it('sin restricción: "Todas las sedes"', () => {
    expect(locationAccessLabel({ restrictedToLocations: false, allowedLocationIds: [] })).toBe('Todas las sedes')
  })
  it('con restricción: "1 sede" / "N sedes"', () => {
    expect(locationAccessLabel({ restrictedToLocations: true, allowedLocationIds: ['a'] })).toBe('1 sede')
    expect(locationAccessLabel({ restrictedToLocations: true, allowedLocationIds: ['a', 'b', 'c'] })).toBe('3 sedes')
  })
})

describe('validateLocationAccess / toAllowedLocationIds', () => {
  it('"some" sin sedes es inválido; con sedes o en "all" es válido', () => {
    expect(validateLocationAccess('some', [])).toBe(LOCATION_ACCESS_REQUIRED_MESSAGE)
    expect(validateLocationAccess('some', ['a'])).toBeNull()
    expect(validateLocationAccess('all', [])).toBeNull()
  })
  it('"all" => null (sin restricción); "some" => copia de la selección', () => {
    expect(toAllowedLocationIds('all', ['a'])).toBeNull()
    const ids = ['a', 'b']
    const result = toAllowedLocationIds('some', ids)
    expect(result).toEqual(['a', 'b'])
    expect(result).not.toBe(ids)
  })
})

describe('useClientLocationAccess', () => {
  beforeEach(() => {
    vi.mocked(listClientLocations).mockReset()
  })

  it('por defecto: todas las sedes, sin error de validación', () => {
    const a = useClientLocationAccess()
    expect(a.mode.value).toBe('all')
    expect(a.validationError.value).toBeNull()
    expect(a.allowedLocationIds.value).toBeNull()
  })

  it('resetForClient carga solo las sedes activas y reinicia la selección', async () => {
    vi.mocked(listClientLocations).mockResolvedValue(respond([loc('1'), loc('2', false)]))
    const a = useClientLocationAccess()
    a.mode.value = 'some'
    a.selectedIds.value = ['x']
    await a.resetForClient('c1')
    expect(listClientLocations).toHaveBeenCalledWith('c1')
    expect(a.mode.value).toBe('all')
    expect(a.selectedIds.value).toEqual([])
    expect(a.options.value.map((l) => l.id)).toEqual(['1'])
  })

  it('resetForClient(null) limpia sin consultar', async () => {
    const a = useClientLocationAccess()
    await a.resetForClient(null)
    expect(listClientLocations).not.toHaveBeenCalled()
    expect(a.options.value).toEqual([])
  })

  it('"some" sin selección es inválido y con selección expone los ids', async () => {
    const a = useClientLocationAccess()
    a.mode.value = 'some'
    expect(a.validationError.value).toBe(LOCATION_ACCESS_REQUIRED_MESSAGE)
    a.selectedIds.value = ['1']
    expect(a.validationError.value).toBeNull()
    expect(a.allowedLocationIds.value).toEqual(['1'])
  })

  it('preload precarga modo y selección, y conserva visibles las sedes inactivas ya permitidas', async () => {
    vi.mocked(listClientLocations).mockResolvedValue(respond([loc('1'), loc('2', false), loc('3', false)]))
    const a = useClientLocationAccess()
    await a.preload({ clientId: 'c1', restrictedToLocations: true, allowedLocationIds: ['2'] })
    expect(a.mode.value).toBe('some')
    expect(a.selectedIds.value).toEqual(['2'])
    expect(a.options.value.map((l) => l.id)).toEqual(['1', '2'])
  })

  it('preload de un usuario sin restricción queda en "all"', async () => {
    vi.mocked(listClientLocations).mockResolvedValue(respond([loc('1')]))
    const a = useClientLocationAccess()
    await a.preload({ clientId: 'c1', restrictedToLocations: false, allowedLocationIds: [] })
    expect(a.mode.value).toBe('all')
    expect(a.selectedIds.value).toEqual([])
  })

  it('si falla la carga registra el error y marca loadFailed', async () => {
    const err = new Error('boom')
    vi.mocked(listClientLocations).mockRejectedValue(err)
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const a = useClientLocationAccess()
    await a.resetForClient('c1')
    expect(spy).toHaveBeenCalledWith(expect.any(String), err)
    expect(a.loadFailed.value).toBe(true)
    expect(a.loading.value).toBe(false)
    spy.mockRestore()
  })

  it('descarta la respuesta de una empresa anterior si se cambió de empresa mientras cargaba', async () => {
    let resolveFirst!: (v: any) => void
    vi.mocked(listClientLocations)
      .mockReturnValueOnce(new Promise((r) => (resolveFirst = r)) as any)
      .mockResolvedValueOnce(respond([loc('B')]))
    const a = useClientLocationAccess()
    const first = a.resetForClient('c1')
    await a.resetForClient('c2')
    resolveFirst(respond([loc('A')]))
    await first
    expect(a.options.value.map((l) => l.id)).toEqual(['B'])
  })
})
