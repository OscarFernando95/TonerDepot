import { readFileSync } from 'node:fs'
import { describe, expect, it } from 'vitest'

// AUDIT.md H-22: protege la CSP preparada en hosting/ de un debilitamiento accidental. No prueba el navegador (eso
// se verificó con Chrome real, ver hosting/README.md), solo que la política no pierda sus garantías.
function load(name: string) {
  return JSON.parse(readFileSync(new URL(`./${name}`, import.meta.url), 'utf8'))
}

const files = [
  ['staticwebapp.config.csp-enforce.json', 'Content-Security-Policy'],
  ['staticwebapp.config.csp-report-only.json', 'Content-Security-Policy-Report-Only']
] as const

describe.each(files)('%s', (name, header) => {
  const config = load(name)
  const policy: string = config.globalHeaders[header]
  const directive = (d: string) => policy.split(';').map((x) => x.trim()).find((x) => x.startsWith(d + ' ') || x === d)

  it('usa la cabecera esperada y solo esa', () => {
    expect(policy).toBeTruthy()
    const other = header === 'Content-Security-Policy' ? 'Content-Security-Policy-Report-Only' : 'Content-Security-Policy'
    expect(config.globalHeaders[other]).toBeUndefined()
  })

  it('script-src no admite inline ni eval', () => {
    expect(directive('script-src')).toBe("script-src 'self'")
    expect(policy).not.toContain("'unsafe-eval'")
  })

  it("'unsafe-inline' solo existe para atributos style", () => {
    const withInline = policy.split(';').map((x) => x.trim()).filter((x) => x.includes("'unsafe-inline'"))
    expect(withInline).toEqual(["style-src-attr 'unsafe-inline'"])
  })

  it('cierra plugins, incrustación y base-uri', () => {
    expect(directive('object-src')).toBe("object-src 'none'")
    expect(directive('frame-ancestors')).toBe("frame-ancestors 'none'")
    expect(directive('base-uri')).toBe("base-uri 'self'")
  })

  it('no hay comodines en las directivas de recursos activos', () => {
    for (const d of ['script-src', 'connect-src', 'default-src']) {
      expect(directive(d)).not.toMatch(/\s\*(\s|$)/)
    }
  })

  it('mantiene el fallback del router y la geolocalización del check-in', () => {
    expect(config.navigationFallback.rewrite).toBe('/index.html')
    expect(config.globalHeaders['Permissions-Policy']).toContain('geolocation=(self)')
  })
})

it('hay un placeholder de API a sustituir antes de activar', () => {
  expect(load('staticwebapp.config.csp-enforce.json').globalHeaders['Content-Security-Policy']).toContain('<API_HOST>')
})
