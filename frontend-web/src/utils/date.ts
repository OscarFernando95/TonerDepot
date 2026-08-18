// Instanciar Intl.DateTimeFormat es caro y estas dos variantes se llaman una vez por celda de
// tabla — a nivel de módulo, se crean una sola vez por sesión en vez de una vez por render
// (CODE_QUALITY_AUDIT.md hallazgo #12).

// Para fechas de calendario puro (vigencia de contratos): fuerza UTC para que la fecha mostrada
// no dependa de la zona horaria del navegador.
const utcDateFormatter = new Intl.DateTimeFormat(undefined, { timeZone: 'UTC' })

// Para instantes reales (última/próxima fecha de mantenimiento): en la zona horaria local, como
// ya hacía el código sin opciones explícitas.
const localDateFormatter = new Intl.DateTimeFormat()

export function formatDateUTC(value: string | Date): string {
  return utcDateFormatter.format(new Date(value))
}

export function formatDateLocal(value: string | Date): string {
  return localDateFormatter.format(new Date(value))
}
