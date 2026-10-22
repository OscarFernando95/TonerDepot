import { computed, reactive } from 'vue'
import type { CreateManualMaintenanceOrderRequest } from '../api/types'
import { formatPlannedAt, toPlannedIso, validatePlannedAt } from './useMaintenanceSchedule'

// Qué se hará en un mantenimiento a demanda: cualquier combinación no vacía de general, unidades e insumos, más el
// motivo opcional y, solo para el técnico, cuándo (ahora o programado). Lo comparten el staff al pedirlo (AssetDetailView)
// y el técnico al iniciarlo por su cuenta (MeterReadingsView) vía MaintenanceChoiceDialog. Mismas reglas que
// CreateManualMaintenanceOrderRequestValidator.cs.
export type MaintenanceChoice = Pick<
  CreateManualMaintenanceOrderRequest,
  'includesGeneral' | 'includesUnits' | 'includesConsumables'
> & {
  reason: string | null
  // Solo presente si se eligió "Programarlo para otra fecha": ISO UTC. Sin ella el request queda como siempre.
  plannedAt?: string
}

export type MaintenanceWhen = 'now' | 'later'

export function useMaintenanceChoice(initialGeneral = false) {
  const form = reactive({
    includesGeneral: initialGeneral,
    includesUnits: false,
    includesConsumables: false,
    reason: '',
    when: 'now' as MaintenanceWhen,
    plannedAt: null as Date | null
  })

  const scheduling = computed(() => form.when === 'later')
  // Error de la fecha elegida (null si es válida o si todavía no se ha tocado el picker).
  const plannedError = computed(() => (scheduling.value && form.plannedAt ? validatePlannedAt(form.plannedAt) : null))
  const scheduleValid = computed(() => !scheduling.value || (!!form.plannedAt && validatePlannedAt(form.plannedAt) === null))

  const valid = computed(
    () => (form.includesGeneral || form.includesUnits || form.includesConsumables) && scheduleValid.value
  )

  function reset() {
    form.includesGeneral = initialGeneral
    form.includesUnits = false
    form.includesConsumables = false
    form.reason = ''
    form.when = 'now'
    form.plannedAt = null
  }

  function toChoice(): MaintenanceChoice {
    const choice: MaintenanceChoice = {
      includesGeneral: form.includesGeneral,
      includesUnits: form.includesUnits,
      includesConsumables: form.includesConsumables,
      reason: form.reason.trim() || null
    }
    // Con "Ahora" la clave ni existe: el request es idéntico al de antes.
    if (scheduling.value && form.plannedAt) choice.plannedAt = toPlannedIso(form.plannedAt)
    return choice
  }

  return { form, valid, scheduling, plannedError, reset, toChoice }
}

// Texto del botón de confirmar: con fecha el diálogo ya no "inicia", "programa".
export function confirmButtonLabel(baseLabel: string, scheduled: boolean): string {
  return scheduled ? 'Programar mantenimiento' : baseLabel
}

// Mensaje de éxito tras crear la orden. Sin fecha, el de siempre (hay que hacer el check-in en Mi trabajo).
export function maintenanceStartedMessage(plannedAtIso: string | null | undefined, now: Date = new Date()): string {
  return plannedAtIso
    ? `Mantenimiento programado para ${formatPlannedAt(plannedAtIso, now)}. El cliente verá la fecha.`
    : 'Mantenimiento listo. Haz el check-in en Mi trabajo.'
}
