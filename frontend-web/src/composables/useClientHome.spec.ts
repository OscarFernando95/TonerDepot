import { describe, expect, it } from 'vitest'
import type { ClientHomeDto } from '../api/types'
import {
  ageLabel,
  commentsNotice,
  greeting,
  homeSummary,
  installationsTile,
  machinesTile,
  maintenanceTile,
  priorityLabel,
  priorityLamp,
  splitVisible,
  ticketStatusLabel,
  ticketStatusLamp,
  ticketsTile,
  toneLamp,
  visitLabel
} from './useClientHome'

function makeHome(overrides: Partial<ClientHomeDto> = {}): ClientHomeDto {
  return {
    clientName: 'Acme S.A.S.',
    locationCount: 2,
    openTickets: 0,
    ticketsInProgress: 0,
    openMaintenance: 0,
    maintenanceScheduled: 0,
    maintenanceInProgress: 0,
    maintenanceWithoutVisitDate: 0,
    pendingInstallations: 0,
    installationsInProgress: 0,
    installedAssets: 0,
    assetsInMaintenance: 0,
    assetsWithOpenTicket: 0,
    recentTickets: [],
    upcomingMaintenance: [],
    installations: [],
    ...overrides
  }
}

const ticket = (status: string, priority: string) =>
  ({
    id: 't',
    equipment: 'Ricoh MP 301',
    locationName: 'Sede Norte',
    description: 'No imprime',
    status,
    priority,
    technicianName: null,
    createdAt: '2026-10-22T10:00:00Z'
  }) as ClientHomeDto['recentTickets'][number]

describe('greeting', () => {
  it('cambia según la hora del día', () => {
    expect(greeting(8)).toBe('Buenos días')
    expect(greeting(12)).toBe('Buenas tardes')
    expect(greeting(18)).toBe('Buenas tardes')
    expect(greeting(19)).toBe('Buenas noches')
  })
})

describe('homeSummary', () => {
  it('confirma que no hay nada pendiente', () => {
    expect(homeSummary(makeHome())).toMatch(/Todo en orden/)
  })

  it('enumera lo que está en marcha con singular y plural correctos', () => {
    const text = homeSummary(makeHome({ openTickets: 2, openMaintenance: 1, pendingInstallations: 1 }))
    expect(text).toBe('Ahora mismo tienes 2 tickets abiertos, 1 mantenimiento y 1 instalación pendiente.')
  })

  it('con una sola cosa no lleva "y"', () => {
    expect(homeSummary(makeHome({ openTickets: 1 }))).toBe('Ahora mismo tienes 1 ticket abierto.')
  })
})

describe('toneLamp', () => {
  it('cada tono tiene su lámpara (nunca solo color)', () => {
    expect(toneLamp('calm')).toBe('on-time')
    expect(toneLamp('info')).toBe('on-time')
    expect(toneLamp('attention')).toBe('warning')
    expect(toneLamp('alert')).toBe('critical')
  })
})

describe('ageLabel', () => {
  const now = new Date('2026-10-22T12:00:00Z').getTime()
  it('expresa minutos, horas y días', () => {
    expect(ageLabel('2026-10-22T11:59:40Z', now)).toBe('ahora')
    expect(ageLabel('2026-10-22T11:45:00Z', now)).toBe('hace 15 min')
    expect(ageLabel('2026-10-22T09:00:00Z', now)).toBe('hace 3 h')
    expect(ageLabel('2026-10-21T11:00:00Z', now)).toBe('hace 1 día')
    expect(ageLabel('2026-10-19T12:00:00Z', now)).toBe('hace 3 días')
  })
  it('devuelve vacío si la fecha es inválida', () => {
    expect(ageLabel('no-es-fecha', now)).toBe('')
  })
})

describe('splitVisible', () => {
  it('recorta y cuenta lo que quedó fuera', () => {
    expect(splitVisible([1, 2, 3, 4, 5], 3)).toEqual({ shown: [1, 2, 3], hidden: 2 })
    expect(splitVisible([1], 3)).toEqual({ shown: [1], hidden: 0 })
  })
})

describe('etiquetas de ticket', () => {
  it('traduce estados y muestra Crítica con tilde', () => {
    expect(ticketStatusLabel('SinAsignar')).toBe('Sin asignar')
    expect(ticketStatusLabel('EnProceso')).toBe('En proceso')
    expect(ticketStatusLabel('constructor')).toBe('constructor')
    expect(priorityLabel('Critica')).toBe('Crítica')
    expect(priorityLabel('Alta')).toBe('Alta')
  })

  it('lámpara por estado y criticidad', () => {
    expect(ticketStatusLamp('Abierto')).toBe('warning')
    expect(ticketStatusLamp('EnProceso')).toBe('on-time')
    expect(ticketStatusLamp('Cancelado')).toBe('idle')
    expect(priorityLamp('Critica')).toBe('critical')
    expect(priorityLamp('Alta')).toBe('warning')
    expect(priorityLamp('Media')).toBe('idle')
  })
})

describe('ticketsTile', () => {
  it('sin tickets abiertos es calmado y vacío, con frase cálida', () => {
    const t = ticketsTile(makeHome())
    expect(t).toMatchObject({ tone: 'calm', empty: true, value: 0 })
    expect(t.phrase).toBe('Todo en orden: no tienes tickets abiertos.')
  })

  it('con tickets en curso es informativo', () => {
    const t = ticketsTile(makeHome({ openTickets: 3, ticketsInProgress: 1, recentTickets: [ticket('EnProceso', 'Media')] }))
    expect(t).toMatchObject({ tone: 'info', value: 3, inProgress: 1, empty: false })
    expect(t.phrase).toBe('1 en curso; el resto espera atención.')
  })

  it('ninguno en curso pide atención suave', () => {
    const t = ticketsTile(makeHome({ openTickets: 2, ticketsInProgress: 0, recentTickets: [ticket('Abierto', 'Media')] }))
    expect(t.tone).toBe('attention')
  })

  it('un ticket crítico esperando técnico sube a alerta; uno alto, a atención', () => {
    expect(ticketsTile(makeHome({ openTickets: 1, ticketsInProgress: 0, recentTickets: [ticket('SinAsignar', 'Critica')] })).tone).toBe('alert')
    expect(ticketsTile(makeHome({ openTickets: 2, ticketsInProgress: 1, recentTickets: [ticket('Abierto', 'Alta')] })).tone).toBe('attention')
  })

  it('un crítico que ya se está atendiendo no grita', () => {
    expect(ticketsTile(makeHome({ openTickets: 1, ticketsInProgress: 1, recentTickets: [ticket('EnProceso', 'Critica')] })).tone).toBe('info')
  })

  it('acota "en curso" a los abiertos si los contadores llegan desfasados', () => {
    expect(ticketsTile(makeHome({ openTickets: 1, ticketsInProgress: 5 })).inProgress).toBe(1)
  })
})

describe('visitLabel', () => {
  const now = new Date(2026, 9, 22, 9, 0)
  it('muestra la visita en hora local o pide confirmar la fecha', () => {
    const planned = new Date(2026, 9, 30, 10, 0).toISOString()
    const text = visitLabel({ plannedAt: planned }, now)
    expect(text).toMatch(/^Visita: /)
    expect(text).toMatch(/30/)
    expect(text).toMatch(/10:00/)
    expect(visitLabel({ plannedAt: null }, now)).toBe('Fecha por confirmar')
    expect(visitLabel({ plannedAt: 'basura' }, now)).toBe('Fecha por confirmar')
  })
})

describe('commentsNotice', () => {
  it('es null sin comentarios y concuerda en número', () => {
    expect(commentsNotice([{ commentCount: 0 }])).toBeNull()
    expect(commentsNotice([{ commentCount: 1 }])).toBe('Hay 1 comentario en tus mantenimientos.')
    expect(commentsNotice([{ commentCount: 1 }, { commentCount: 2 }])).toBe('Hay 3 comentarios en tus mantenimientos.')
  })
})

describe('maintenanceTile', () => {
  it('sin mantenimientos es calmado y vacío', () => {
    expect(maintenanceTile(makeHome())).toMatchObject({ tone: 'calm', empty: true })
  })

  it('en curso tiene prioridad en la frase', () => {
    const t = maintenanceTile(makeHome({ openMaintenance: 2, maintenanceInProgress: 1, maintenanceScheduled: 1 }))
    expect(t).toMatchObject({ tone: 'info', empty: false, inProgress: 1, scheduled: 1 })
    expect(t.phrase).toBe('Un técnico está haciendo el mantenimiento ahora.')
  })

  it('solo esperando fecha lo dice', () => {
    const t = maintenanceTile(makeHome({ openMaintenance: 1, maintenanceWithoutVisitDate: 1 }))
    expect(t.withoutDate).toBe(1)
    expect(t.phrase).toBe('Estamos coordinando la fecha de la visita.')
  })
})

describe('installationsTile', () => {
  it('sin instalaciones es compacto y calmado', () => {
    expect(installationsTile(makeHome())).toMatchObject({ tone: 'calm', empty: true, phrase: 'Sin instalaciones pendientes' })
  })

  it('distingue instalando ahora de pendientes', () => {
    expect(installationsTile(makeHome({ pendingInstallations: 2, installationsInProgress: 1 })).phrase).toBe('Estamos instalando un equipo ahora.')
    expect(installationsTile(makeHome({ pendingInstallations: 2 })).phrase).toBe('Tienes 2 equipos pendientes de instalación.')
  })
})

describe('machinesTile', () => {
  it('sin máquinas instaladas', () => {
    expect(machinesTile(makeHome())).toMatchObject({ empty: true, installed: 0, inOrderPct: 0, attentionPct: 0, tone: 'calm' })
  })

  it('todas en orden es calmado', () => {
    const t = machinesTile(makeHome({ installedAssets: 4 }))
    expect(t).toMatchObject({ inOrder: 4, attention: 0, inOrderPct: 100, attentionPct: 0, tone: 'calm' })
    expect(t.phrase).toBe('Todas tus máquinas están en orden.')
  })

  it('en orden = instaladas menos las que tienen ticket abierto', () => {
    const t = machinesTile(makeHome({ installedAssets: 4, assetsWithOpenTicket: 1, assetsInMaintenance: 1 }))
    expect(t).toMatchObject({ inOrder: 3, attention: 1, inOrderPct: 75, attentionPct: 25, inMaintenance: 1, tone: 'attention' })
    expect(t.phrase).toBe('3 de 4 en orden; 1 con ticket abierto.')
  })

  it('los porcentajes siempre suman 100', () => {
    const t = machinesTile(makeHome({ installedAssets: 3, assetsWithOpenTicket: 1 }))
    expect(t.inOrderPct + t.attentionPct).toBe(100)
  })

  it('acota si hay más tickets que máquinas', () => {
    const t = machinesTile(makeHome({ installedAssets: 2, assetsWithOpenTicket: 9 }))
    expect(t).toMatchObject({ inOrder: 0, attention: 2, inOrderPct: 0, attentionPct: 100 })
  })
})
