// Mensaje presentable a partir de un error de la API (ProblemDetails de ExceptionHandlingMiddleware).
// Un 400 de validación trae el motivo concreto en `errors` por campo; su `title` es solo "Uno o más campos no son
// válidos.". Sin esto, una vista que solo leía `title` mostraba ese genérico en vez de, p. ej., "El área es
// obligatoria para completar la instalación." (AUDIT.md H-37). Mismo criterio que ApiClient.translate en la app.
export function apiErrorMessage(err: any, fallback: string): string {
  const data = err?.response?.data
  const errors = data?.errors
  if (errors && typeof errors === 'object') {
    const detail = Object.values(errors).flat().filter(Boolean).join(' ')
    if (detail) return detail
  }
  return data?.title || fallback
}
