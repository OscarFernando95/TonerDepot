import { computed, type Ref } from 'vue'

// Espejo EXACTO de ChangePasswordRequestValidator.cs (backend/src/Toner.Application/Auth/Validators).
// Si esas reglas cambian, hay que actualizar también acá y el test de paridad en
// usePasswordRules.spec.ts — de lo contrario el checklist visual y el botón de guardar quedan
// mintiendo sobre lo que el backend realmente va a aceptar.
export interface PasswordRule {
  key: string
  label: string
  test: (password: string) => boolean
}

export const passwordRules: PasswordRule[] = [
  { key: 'length', label: 'Mínimo 10 caracteres', test: (pw) => pw.length >= 10 },
  { key: 'upper', label: 'Al menos una letra mayúscula', test: (pw) => /[A-Z]/.test(pw) },
  { key: 'lower', label: 'Al menos una letra minúscula', test: (pw) => /[a-z]/.test(pw) },
  { key: 'number', label: 'Al menos un número', test: (pw) => /[0-9]/.test(pw) }
]

export function isPasswordValid(password: string): boolean {
  return passwordRules.every((rule) => rule.test(password))
}

export function usePasswordRuleStatus(password: Ref<string>) {
  return computed(() => passwordRules.map((rule) => ({ ...rule, met: rule.test(password.value) })))
}
