// Teléfonos: exactamente 10 dígitos numéricos, sin letras, espacios ni símbolos. La misma regla vive en
// los validators del backend (PhoneRules.cs) — esta copia solo evita el viaje de ida y vuelta al usuario.
export const PHONE_LENGTH = 10

export const PHONE_ERROR_MESSAGE = `El teléfono debe tener exactamente ${PHONE_LENGTH} dígitos numéricos.`

// Para :formatter y :parser de <el-input>: descarta todo lo que no sea dígito y corta a 10.
export function digitsOnly(value: string): string {
  return value.replace(/\D/g, '').slice(0, PHONE_LENGTH)
}

// Vacío es válido salvo que el campo sea obligatorio (`required`).
export function isValidPhone(value: string | null | undefined, required = false): boolean {
  if (!value) return !required
  return new RegExp(`^\\d{${PHONE_LENGTH}}$`).test(value)
}
