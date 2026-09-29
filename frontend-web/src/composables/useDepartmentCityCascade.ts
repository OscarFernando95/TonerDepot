import { computed, ref, watch } from 'vue'
import type { Ref } from 'vue'
import type { CityDto } from '../api/types'

// Cascada Departamento→Ciudad, reutilizada en cualquier formulario que pida
// una ciudad (sedes de cliente, cobertura de técnico, etc.) — agrupa por
// stateOrProvince así el selector de ciudad no es una lista plana de
// cientos de opciones. `departmentName` es local al formulario que lo usa,
// nunca se manda al backend (ver CLAUDE.md, convención de composables para
// lógica repetida entre vistas).
export function useDepartmentCityCascade(cities: Ref<CityDto[]>, onDepartmentChange: () => void) {
  const departmentName = ref('')
  const departments = computed(() => [...new Set(cities.value.map((c) => c.stateOrProvince))].sort())
  const citiesInDepartment = computed(() => cities.value.filter((c) => c.stateOrProvince === departmentName.value))

  watch(departmentName, onDepartmentChange)

  // Pre-popula el departamento a partir de una ciudad ya elegida (ej. al
  // abrir un diálogo de edición). El watch de arriba limpia el cityId del
  // formulario de forma asíncrona (próximo tick) — el caller debe esperar
  // un nextTick() antes de fijar el cityId real después de llamar a esto,
  // o el watch lo pisaría.
  function setDepartmentForCity(cityId: string | null | undefined) {
    const city = cities.value.find((c) => c.id === cityId)
    departmentName.value = city?.stateOrProvince ?? ''
  }

  return { departmentName, departments, citiesInDepartment, setDepartmentForCity }
}
