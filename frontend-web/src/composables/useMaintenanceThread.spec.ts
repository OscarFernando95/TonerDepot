import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ref } from 'vue'
import type { MaintenanceOrderCommentDto } from '../api/types'

vi.mock('../api/maintenanceOrders', () => ({
  listOrderComments: vi.fn(),
  addOrderComment: vi.fn()
}))

import * as ordersApi from '../api/maintenanceOrders'
import {
  COMMENT_MAX_LENGTH,
  authorRoleLabel,
  formatCommentDate,
  getSeenAt,
  hasUnseenComments,
  isThreadClosed,
  markThreadSeen,
  orderHasNewComments,
  threadEventText,
  useMaintenanceThread,
  validateCommentBody
} from './useMaintenanceThread'

const NOW = new Date(2026, 9, 22, 14, 30)

function entry(over: Partial<MaintenanceOrderCommentDto> = {}): MaintenanceOrderCommentDto {
  return {
    id: 'c1',
    maintenanceOrderId: 'o1',
    kind: 'Comentario',
    body: 'Hola',
    plannedAt: null,
    authorUserId: 'u1',
    authorName: 'Ana',
    authorRole: 'Cliente',
    createdAt: new Date(2026, 9, 22, 14, 0).toISOString(),
    ...over
  }
}

describe('authorRoleLabel', () => {
  it.each([
    ['Cliente', 'Cliente'],
    ['Tecnico', 'Técnico'],
    ['Administrador', 'Administrador'],
    ['Coordinador', 'Coordinador']
  ])('%s -> %s', (role, label) => {
    expect(authorRoleLabel(role)).toBe(label)
  })

  it('un rol desconocido se muestra tal cual, sin heredar del prototipo', () => {
    expect(authorRoleLabel('Auditor')).toBe('Auditor')
    expect(authorRoleLabel('constructor')).toBe('constructor')
  })
})

describe('isThreadClosed', () => {
  it('solo Completada y Cancelada cierran el hilo', () => {
    expect(isThreadClosed('Completada')).toBe(true)
    expect(isThreadClosed('Cancelada')).toBe(true)
    for (const s of ['Pendiente', 'Asignada', 'EnProceso']) expect(isThreadClosed(s)).toBe(false)
  })
})

describe('validateCommentBody', () => {
  it('vacío o solo espacios: inválido y sin mensaje de error', () => {
    expect(validateCommentBody('')).toEqual({ valid: false, length: 0, error: null })
    expect(validateCommentBody('   \n ')).toEqual({ valid: false, length: 0, error: null })
  })

  it('cuenta el largo ya recortado', () => {
    expect(validateCommentBody('  hola  ')).toEqual({ valid: true, length: 4, error: null })
  })

  it('acepta exactamente el máximo y rechaza uno más', () => {
    expect(validateCommentBody('a'.repeat(COMMENT_MAX_LENGTH)).valid).toBe(true)
    const over = validateCommentBody('a'.repeat(COMMENT_MAX_LENGTH + 1))
    expect(over.valid).toBe(false)
    expect(over.error).toMatch(/1000/)
  })
})

describe('formatCommentDate', () => {
  const ago = (ms: number) => new Date(NOW.getTime() - ms).toISOString()

  it('relativa para lo reciente', () => {
    expect(formatCommentDate(ago(10_000), NOW)).toBe('justo ahora')
    expect(formatCommentDate(new Date(NOW.getTime() + 5000).toISOString(), NOW)).toBe('justo ahora')
    expect(formatCommentDate(ago(5 * 60_000), NOW)).toBe('hace 5 min')
    expect(formatCommentDate(ago(3 * 3_600_000 + 1000), NOW)).toBe('hace 3 h')
  })

  it('absoluta desde 24 horas atrás', () => {
    const text = formatCommentDate(new Date(2026, 9, 20, 9, 5).toISOString(), NOW)
    expect(text).toMatch(/20/)
    expect(text).toMatch(/oct/i)
    expect(text).toMatch(/09:05/)
  })

  it('fecha inválida -> guion', () => {
    expect(formatCommentDate('x', NOW)).toBe('—')
  })
})

describe('threadEventText', () => {
  it('describe quién programó y para cuándo', () => {
    const text = threadEventText(entry({ kind: 'Programacion', authorName: 'Juan', plannedAt: new Date(2026, 9, 30, 10, 0).toISOString() }), NOW)
    expect(text).toMatch(/^Juan programó el mantenimiento para el /)
    expect(text).toMatch(/10:00/)
  })

  it('sin fecha cae al texto de la entrada', () => {
    expect(threadEventText(entry({ kind: 'Programacion', body: 'Reprogramado', plannedAt: null }), NOW)).toBe('Reprogramado')
  })
})

describe('comentarios nuevos', () => {
  it('hasUnseenComments compara contra lo último visto', () => {
    expect(hasUnseenComments(null, null)).toBe(false)
    expect(hasUnseenComments('2026-10-22T10:00:00Z', null)).toBe(true)
    expect(hasUnseenComments('2026-10-22T10:00:00Z', '2026-10-22T09:00:00Z')).toBe(true)
    expect(hasUnseenComments('2026-10-22T10:00:00Z', '2026-10-22T10:00:00Z')).toBe(false)
    expect(hasUnseenComments('2026-10-22T10:00:00Z', '2026-10-22T11:00:00Z')).toBe(false)
  })

  it('markThreadSeen solo avanza y orderHasNewComments lo refleja', () => {
    const id = 'orden-seen-1'
    const order = { id, lastCommentAt: '2026-10-22T10:00:00Z' }
    expect(orderHasNewComments(order)).toBe(true)
    markThreadSeen(id, '2026-10-22T10:00:00Z')
    expect(orderHasNewComments(order)).toBe(false)
    markThreadSeen(id, '2026-10-22T08:00:00Z') // más viejo: no retrocede
    expect(getSeenAt(id)).toBe('2026-10-22T10:00:00Z')
    expect(orderHasNewComments({ id, lastCommentAt: '2026-10-22T12:00:00Z' })).toBe(true)
  })
})

describe('useMaintenanceThread', () => {
  beforeEach(() => {
    vi.resetAllMocks()
    vi.spyOn(console, 'error').mockImplementation(() => {})
  })

  it('carga el hilo y lo marca como visto', async () => {
    const list = [entry({ id: 'a', createdAt: '2026-10-22T10:00:00Z' }), entry({ id: 'b', createdAt: '2026-10-22T11:00:00Z' })]
    vi.mocked(ordersApi.listOrderComments).mockResolvedValue({ data: list } as any)
    const t = useMaintenanceThread(ref('orden-load'))
    await t.load()
    expect(t.entries.value).toHaveLength(2)
    expect(t.loading.value).toBe(false)
    expect(getSeenAt('orden-load')).toBe('2026-10-22T11:00:00Z')
  })

  it('si falla la carga registra el error del objeto y lo indica', async () => {
    const boom = new Error('red')
    vi.mocked(ordersApi.listOrderComments).mockRejectedValue(boom)
    const t = useMaintenanceThread(ref('orden-fail'))
    await t.load()
    expect(t.loadFailed.value).toBe(true)
    expect(console.error).toHaveBeenCalledWith(expect.any(String), boom)
  })

  it('no envía si el borrador está vacío', async () => {
    const t = useMaintenanceThread(ref('orden-empty'))
    expect(t.canSend.value).toBe(false)
    expect(await t.send()).toBe(false)
    expect(ordersApi.addOrderComment).not.toHaveBeenCalled()
  })

  it('envía el texto recortado, lo agrega al hilo y limpia el borrador', async () => {
    const created = entry({ id: 'n', body: 'Ese día no puedo', createdAt: '2026-10-22T12:00:00Z' })
    vi.mocked(ordersApi.addOrderComment).mockResolvedValue({ data: created } as any)
    const t = useMaintenanceThread(ref('orden-send'))
    t.draft.value = '  Ese día no puedo  '
    expect(t.canSend.value).toBe(true)
    expect(await t.send()).toBe(true)
    expect(ordersApi.addOrderComment).toHaveBeenCalledWith('orden-send', 'Ese día no puedo')
    expect(t.entries.value.map((e) => e.id)).toEqual(['n'])
    expect(t.draft.value).toBe('')
    expect(t.sending.value).toBe(false)
  })

  it('si el envío falla conserva el borrador y muestra el mensaje del servidor', async () => {
    const err = { response: { status: 409, data: { title: 'La orden está cerrada.' } } }
    vi.mocked(ordersApi.addOrderComment).mockRejectedValue(err)
    const t = useMaintenanceThread(ref('orden-409'))
    t.draft.value = 'hola'
    expect(await t.send()).toBe(false)
    expect(t.draft.value).toBe('hola')
    expect(t.sendError.value).toBe('La orden está cerrada.')
    expect(console.error).toHaveBeenCalledWith(expect.any(String), err)
  })
})
