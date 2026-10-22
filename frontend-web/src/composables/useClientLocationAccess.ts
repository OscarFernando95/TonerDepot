import { computed, ref } from 'vue'
import { listClientLocations } from '../api/clientLocations'
import type { ClientLocationDto } from '../api/types'

export type LocationAccessMode = 'all' | 'some'

export const LOCATION_ACCESS_REQUIRED_MESSAGE = 'Elige al menos una sede, o cambia a "Todas las sedes de la empresa".'

// Etiqueta compacta para la lista de usuarios Cliente: "Todas las sedes" / "1 sede" / "N sedes".
export function locationAccessLabel(user: { restrictedToLocations: boolean; allowedLocationIds: string[] }): string {
  if (!user.restrictedToLocations) return 'Todas las sedes'
  const n = user.allowedLocationIds.length
  return n === 1 ? '1 sede' : `${n} sedes`
}

// Mensaje de validación del formulario, o null si es válido. Espejo de la regla del backend:
// restringir exige al menos una sede.
export function validateLocationAccess(mode: LocationAccessMode, selectedIds: string[]): string | null {
  return mode === 'some' && selectedIds.length === 0 ? LOCATION_ACCESS_REQUIRED_MESSAGE : null
}

// Valor para allowedLocationIds en la API: null = sin restricción.
export function toAllowedLocationIds(mode: LocationAccessMode, selectedIds: string[]): string[] | null {
  return mode === 'some' ? [...selectedIds] : null
}

// Estado del control "Acceso a sedes" de un usuario Cliente, compartido por el alta de usuario y el
// diálogo "Sedes de acceso" de la lista.
export function useClientLocationAccess() {
  const mode = ref<LocationAccessMode>('all')
  const selectedIds = ref<string[]>([])
  const locations = ref<ClientLocationDto[]>([])
  const loading = ref(false)
  const loadFailed = ref(false)

  // Descarta respuestas de una empresa anterior si el usuario cambió de empresa mientras cargaba.
  let loadToken = 0

  // Sedes activas de la empresa, más las inactivas que ya estaban seleccionadas al precargar (para que
  // el administrador las vea y pueda quitarlas en vez de que desaparezcan en silencio).
  const preselected = ref<string[]>([])
  const options = computed(() =>
    locations.value.filter((l) => l.isActive || preselected.value.includes(l.id))
  )

  const validationError = computed(() => validateLocationAccess(mode.value, selectedIds.value))
  const allowedLocationIds = computed(() => toAllowedLocationIds(mode.value, selectedIds.value))

  function reset() {
    loadToken++
    mode.value = 'all'
    selectedIds.value = []
    preselected.value = []
    locations.value = []
    loading.value = false
    loadFailed.value = false
  }

  async function loadLocations(clientId: string) {
    const token = ++loadToken
    loading.value = true
    loadFailed.value = false
    try {
      const { data } = await listClientLocations(clientId)
      if (token !== loadToken) return
      locations.value = data
    } catch (err) {
      if (token !== loadToken) return
      console.error('No se pudieron cargar las sedes de la empresa', err)
      locations.value = []
      loadFailed.value = true
    } finally {
      if (token === loadToken) loading.value = false
    }
  }

  // Alta: cambió la empresa o el rol. Reinicia la selección y, si hay empresa, carga sus sedes.
  async function resetForClient(clientId: string | null) {
    reset()
    if (clientId) await loadLocations(clientId)
  }

  // Edición: precarga con el estado actual del usuario.
  async function preload(user: { clientId: string | null; restrictedToLocations: boolean; allowedLocationIds: string[] }) {
    reset()
    mode.value = user.restrictedToLocations ? 'some' : 'all'
    selectedIds.value = user.restrictedToLocations ? [...user.allowedLocationIds] : []
    preselected.value = [...selectedIds.value]
    if (user.clientId) await loadLocations(user.clientId)
  }

  return {
    mode,
    selectedIds,
    options,
    loading,
    loadFailed,
    validationError,
    allowedLocationIds,
    reset,
    resetForClient,
    preload
  }
}

export type ClientLocationAccess = ReturnType<typeof useClientLocationAccess>
