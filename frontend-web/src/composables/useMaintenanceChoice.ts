import { computed, reactive } from 'vue'
import type { CreateManualMaintenanceOrderRequest } from '../api/types'

// Qué se hará en un mantenimiento a demanda: cualquier combinación no vacía de general, unidades e insumos, más el
// motivo opcional. Lo comparten el staff al pedirlo (AssetDetailView) y el técnico al iniciarlo por su cuenta
// (MeterReadingsView) vía MaintenanceChoiceDialog. Mismas reglas que CreateManualMaintenanceOrderRequestValidator.cs.
export type MaintenanceChoice = Pick<
  CreateManualMaintenanceOrderRequest,
  'includesGeneral' | 'includesUnits' | 'includesConsumables'
> & { reason: string | null }

export function useMaintenanceChoice(initialGeneral = false) {
  const form = reactive({ includesGeneral: initialGeneral, includesUnits: false, includesConsumables: false, reason: '' })

  const valid = computed(() => form.includesGeneral || form.includesUnits || form.includesConsumables)

  function reset() {
    form.includesGeneral = initialGeneral
    form.includesUnits = false
    form.includesConsumables = false
    form.reason = ''
  }

  function toChoice(): MaintenanceChoice {
    return {
      includesGeneral: form.includesGeneral,
      includesUnits: form.includesUnits,
      includesConsumables: form.includesConsumables,
      reason: form.reason.trim() || null
    }
  }

  return { form, valid, reset, toChoice }
}
