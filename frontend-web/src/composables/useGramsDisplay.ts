import { ref, watch } from 'vue'

// El revelador se guarda y se descuenta SIEMPRE en gramos; esto solo decide cómo se muestra:
//   'g'  → "1.250 g"
//   'kg' → "1,25 kg (2 bolsas + 250 g)"
export type GramsMode = 'g' | 'kg'

const STORAGE_KEY = 'toner.gramsMode'

function readStoredMode(): GramsMode {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'kg' ? 'kg' : 'g'
  } catch {
    return 'g'
  }
}

// Compartido por toda la app: cambiarlo en una pantalla lo cambia en todas.
export const gramsMode = ref<GramsMode>(readStoredMode())

watch(gramsMode, (mode) => {
  try {
    localStorage.setItem(STORAGE_KEY, mode)
  } catch {
    // Sin almacenamiento (ventana privada): la preferencia vale solo para esta sesión.
  }
})

const gramsFormat = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 0 })
const kgFormat = new Intl.NumberFormat('es-CO', { maximumFractionDigits: 3 })

function pluralBags(count: number) {
  return `${gramsFormat.format(count)} ${count === 1 ? 'bolsa' : 'bolsas'}`
}

export function formatGrams(grams: number, gramsPerBag: number | null | undefined, mode: GramsMode = gramsMode.value): string {
  if (mode === 'g') return `${gramsFormat.format(grams)} g`

  const sign = grams < 0 ? '-' : ''
  const abs = Math.abs(grams)
  const kg = `${sign}${kgFormat.format(abs / 1000)} kg`
  if (!gramsPerBag || gramsPerBag <= 0) return kg

  const bags = Math.floor(abs / gramsPerBag)
  const rest = abs - bags * gramsPerBag
  const parts: string[] = []
  if (bags > 0) parts.push(pluralBags(bags))
  if (rest > 0) parts.push(`${gramsFormat.format(rest)} g`)
  return parts.length === 0 ? kg : `${kg} (${sign}${parts.join(' + ')})`
}

// Cantidad de un ítem: gramos del revelador (según el modo elegido) o el número tal cual para el resto.
export function formatQuantity(quantity: number, gramsPerBag: number | null | undefined, mode: GramsMode = gramsMode.value): string {
  return gramsPerBag ? formatGrams(quantity, gramsPerBag, mode) : String(quantity)
}
