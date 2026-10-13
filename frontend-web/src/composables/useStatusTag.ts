// Color del <el-tag> según el estado de cada entidad. Antes cada vista tenía su propia copia de este
// switch (AUDIT.md H-24) y empezaron a divergir: p. ej. "Cancelado" de un contrato era rojo en la vista
// de staff y gris en el portal del cliente. Un único mapa por entidad evita que vuelva a pasar.

export type TagType = 'primary' | 'success' | 'warning' | 'info' | 'danger'

interface TagMap {
  // Estados conocidos; cualquier otro (p. ej. un valor nuevo del backend) cae en `fallback`.
  types: Record<string, TagType>
  fallback: TagType
}

const TAG_MAPS = {
  ticket: {
    types: { Resuelto: 'success', Cerrado: 'success', Cancelado: 'info', Abierto: 'warning', SinAsignar: 'warning' },
    fallback: 'primary'
  },
  ticketPriority: {
    types: { Critica: 'danger', Alta: 'warning' },
    fallback: 'info'
  },
  maintenanceOrder: {
    types: { Completada: 'success', Cancelada: 'info', Pendiente: 'warning' },
    fallback: 'primary'
  },
  asset: {
    types: { Instalado: 'success', EnMantenimiento: 'warning', PendienteInstalacion: 'warning', DadoDeBaja: 'danger' },
    fallback: 'info'
  },
  contract: {
    types: { Activo: 'success', Vencido: 'warning', Cancelado: 'danger' },
    fallback: 'info'
  },
  technician: {
    types: { Disponible: 'success', Ocupado: 'warning', EnTransito: 'primary' },
    fallback: 'info'
  }
} satisfies Record<string, TagMap>

export type StatusTagEntity = keyof typeof TAG_MAPS

export function statusTagType(entity: StatusTagEntity, status: string): TagType {
  const map: TagMap = TAG_MAPS[entity]
  // hasOwnProperty: sin él, un estado llamado "constructor" o "toString" devolvería la función heredada.
  return Object.prototype.hasOwnProperty.call(map.types, status) ? map.types[status] : map.fallback
}

export function useStatusTag(entity: StatusTagEntity) {
  return { tagType: (status: string) => statusTagType(entity, status) }
}
