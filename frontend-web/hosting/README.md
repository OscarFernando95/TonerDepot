# CSP del front (AUDIT.md H-22) — preparada, NO activada

Estos archivos **no están activos**: Azure Static Web Apps solo lee `staticwebapp.config.json` (en la raíz del
`output_location` desplegado, o en `app_location`/`public/`). Los de esta carpeta se llaman distinto a propósito, así
que desplegar el front tal cual **no** aplica ninguna CSP.

| Archivo | Qué hace |
|---|---|
| `staticwebapp.config.csp-report-only.json` | Mismo conjunto de cabeceras pero con `Content-Security-Policy-Report-Only`: el navegador **avisa** de las violaciones en la consola y **no bloquea nada**. Paso 1 del despliegue. |
| `staticwebapp.config.csp-enforce.json` | `Content-Security-Policy` real. Paso 2, cuando el paso 1 no muestre violaciones en uso real. |

Ambos incluyen además el `navigationFallback` a `index.html` (el router usa `createWebHistory`: sin él, recargar
`/assets` da 404 en Static Web Apps) y cabeceras de endurecimiento: `X-Content-Type-Options`, `Referrer-Policy`,
`Permissions-Policy` (se permite `geolocation=(self)`: el check-in del técnico la usa) y `Cross-Origin-Opener-Policy`.

## Cómo activarla

1. Sustituye `<API_HOST>` (aparece 2 veces) por el host de la API, p. ej. `toner-api.azurewebsites.net`
   (sin `https://` ni `/api`). Cubre las llamadas REST y el WebSocket de SignalR (`wss://`).
2. Copia el archivo elegido a `frontend-web/public/staticwebapp.config.json` (Vite copia `public/` a `dist/`).
3. Despliega, abre la app **con la consola del navegador abierta** y recorre los flujos (login, listados, mapa de
   sedes, fotos de una visita, check-in con geolocalización). En modo *report-only* cualquier violación sale como
   `[Report Only] Refused to …`.
4. Sin violaciones durante unos días → pasa al archivo `enforce`.

## Qué permite cada directiva y por qué

| Directiva | Valor | Motivo |
|---|---|---|
| `default-src` | `'self'` | Todo lo no listado queda cerrado. |
| `script-src` | `'self'` | **Sin `unsafe-inline` ni `unsafe-eval`.** Vite emite módulos externos; no hay scripts en línea. Es la que de verdad frena un XSS. |
| `style-src` | `'self'` | Las hojas de estilo son archivos del build. |
| `style-src-attr` | `'unsafe-inline'` | Solo atributos `style=""`. Vue (`:style`), Element Plus (posicionamiento de popovers) y Leaflet los generan en tiempo de ejecución. No habilita `<style>` ni scripts. |
| `img-src` | `'self' data: blob: https://tile.openstreetmap.org` | `blob:` = fotos de evidencia (`URL.createObjectURL`); OpenStreetMap = teselas del mapa de sedes. |
| `font-src` | `'self' data:` | Inter va empaquetada (`@fontsource-variable/inter`), no viene de Google Fonts. |
| `connect-src` | `'self'`, API (`https` + `wss`), `https://nominatim.openstreetmap.org` | REST, SignalR y la búsqueda de direcciones del mapa. |
| `object-src` | `'none'` | Sin plugins. |
| `base-uri` / `form-action` | `'self'` | Frena la reescritura de la base de URLs y el envío de formularios a otro sitio. |
| `frame-ancestors` | `'none'` | Nadie puede incrustar la app (clickjacking). |
| `upgrade-insecure-requests` | — | Sube a https cualquier recurso http. |

### Si cambia algo de la infraestructura

- **Azure SignalR Service** (docs/escalado-horizontal.md, H-12): añadir a `connect-src` `https://*.service.signalr.net wss://*.service.signalr.net`.
- **Otras teselas de mapa u otro geocodificador:** añadir su host a `img-src` / `connect-src`.
- **Front Door / dominio propio:** no cambia la CSP; sí el `ForwardLimit` de la API (README raíz).

## Cómo se verificó (2026-10-14)

Build de producción servido con la política **aplicada** (no report-only) desde un servidor local con fallback SPA,
Chrome real (puppeteer-core) y la API local. Con `VITE_API_URL=http://localhost:5250/api` y la API reemplazando a
`<API_HOST>`:

- Login + las 15 rutas del menú: **0 violaciones, 0 errores de consola, 0 peticiones fallidas**.
- WebSocket de SignalR: handshake `101` bajo la CSP.
- Mapa de sedes (Leaflet): teselas de `tile.openstreetmap.org` y búsqueda en `nominatim.openstreetmap.org` cargan.
- Selects, popovers y diálogos de Element Plus: funcionan.
- Imagen desde `blob:` (como `EvidenceGallery`): carga.
- **Control negativo:** con una política más estricta (sin `ws:`, sin `blob:`) las mismas pruebas SÍ reportan
  `connect-src` e `img-src`, así que la prueba detecta lo que debe detectar.

No se verificó contra un Azure Static Web Apps real: queda **A VERIFICAR** al desplegar (paso 3).
