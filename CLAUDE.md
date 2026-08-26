# CLAUDE.md — Toner

Convenciones establecidas durante las sesiones de auditoría de seguridad y
calidad (`SECURITY_AUDIT.md`, `SECURITY_AUDIT_V2.md`, `CODE_QUALITY_AUDIT.md`
y el historial de commits del proyecto). Aplican por defecto a cualquier
funcionalidad nueva, sin necesidad de repetirlas en cada prompt.

Cuando una convención de aquí choque con lo que pide un prompt puntual,
señala el choque en una frase y sigue con lo pedido bajo la convención,
salvo que el usuario diga explícitamente lo contrario — **esto aplica
solo a decisiones de estilo/implementación** (cómo nombrar algo, si
extraer a un composable, formato de un endpoint, offset vs. cursor en un
listado nuevo). No aplica a decisiones con consecuencias reales de
seguridad o arquitectura (ej. el trade-off del bloqueo de cuenta tras N
intentos, elegir entre dos diseños de roles de conexión a Postgres,
ampliar o reducir el alcance de RLS, revocación de tokens). Para esas,
la convención por defecto sigue siendo la misma que en toda la auditoría
de esta sesión: detenerse y preguntar. No hay atajo de "seguir bajo la
convención" ahí.

## Backend (.NET 8 + PostgreSQL)

- **Un `SaveChangesAsync()` por operación de negocio.** Cuando una operación
  toca varias entidades a través de varios servicios, los colaboradores
  reciben la entidad ya trackeada (no un `Guid`) y no llaman a
  `SaveChangesAsync()` — el caller que orquesta la operación es quien decide
  cuándo persistir. Precondición implícita de este patrón: todos los
  colaboradores deben compartir la misma instancia *scoped* de `DbContext`;
  no registrar ninguno de estos servicios como singleton. Ver
  `ContractAssetService.AddAsync` + `AssetService.PrepareStatusChangeAsync`
  como referencia, y el hallazgo #8 / `CODE_QUALITY_AUDIT.md` §8.4 para el
  razonamiento completo.

- **RLS en toda tabla con datos multi-cliente.** Política RLS +
  `FORCE ROW LEVEL SECURITY`, sin excepciones — incluida cualquier tabla
  alcanzable transitivamente desde `Clients` (el hallazgo N1/N3 de la V2 fue
  justo una tabla que se quedó sin ella por estar "un salto más lejos").
  - El predicado compara `"ClientId" = NULLIF(current_setting('app.current_client_id', true), '')::uuid`.
    **Nunca `::text`** — el cast a texto vuelve el predicado no-sargable y
    fuerza Seq Scan aunque exista el índice (hallazgo #2).
  - Las políticas van **separadas por rol** (`TO toner_app`,
    `TO toner_app_staff`), no como un único predicado con
    `current_setting('app.is_staff') = 'on' OR "ClientId" = ...`. La versión
    con `OR` tampoco es sargable para Postgres. Ver la migración
    `SplitRlsPoliciesByRole` como plantilla.
  - Una tabla nueva conectada a `Clients` solo por FK indirecta también
    necesita su propia política (con subquery a la tabla padre si no tiene
    `ClientId` propio) — no asumas que hereda el aislamiento del padre.

- **Paginación.** Los listados nuevos devuelven `PagedResult<T>`
  (`Toner.Application/Common/Paging/PagedResult.cs`), nunca una lista suelta.
  - Series temporales sin techo natural (historial de lecturas, logs,
    auditoría) usan cursor/keyset, con desempate por `Id` cuando la columna
    de orden no es única.
  - El resto de listados usa offset (`Page`/`PageSize`).
  - Catálogos de desplegable (ciudades, marcas, estados) no se paginan.
  - Usa `PagedResultFactory.Empty` / `EmptyCursor` para los cortocircuitos
    que devuelven vacío sin llegar a consultar, así el cliente siempre recibe
    el mismo envelope.

- **Validación.** FluentValidation server-side siempre — una validación que
  solo existe en el frontend no cuenta como validación. Para enums que
  vienen como string desde el cliente, `Enum.TryParse<T>(valor, out var x)
  && Enum.IsDefined(x)` (ver `Common/EnumParsing.cs` y los validators de
  `Assets`, `Contracts`, `Tickets`); nunca `Enum.Parse` directo sobre input
  de usuario — lanza y se traduce en 500 en vez de 400.

- **Errores.** Toda `ValidationException` sube por el middleware global y
  sale como 400, nunca como 500 crudo. Excepciones para señalizar errores de
  validación/negocio esperados están bien; usarlas como mecanismo de control
  de flujo para casos que no son excepcionales, no.

- **Logging de seguridad.** Login fallido/exitoso, bloqueo de cuenta,
  acceso denegado, cambios de contraseña: `ILogger` estructurado (ver
  `AuthService`, `UserService`), `Warning` para lo sospechoso,
  `Information` para lo esperado. Nunca loguear contraseñas, hashes de
  contraseña ni tokens completos (JWT truncado o su `jti`, si hace falta
  para correlacionar, sí).

- **Resistencia a oráculos de tiempo.** Cualquier código que compare "el
  recurso existe" vs. "no existe" para un recurso sensible (login por
  cédula/usuario es el caso ya corregido) debe tardar lo mismo en ambos
  caminos — hashear/comparar contra un valor señuelo cuando el recurso no
  existe, en vez de retornar temprano.

- **Secretos.** Nunca hardcodeados ni en `appsettings.*.json` versionado —
  siempre vía configuración/variables de entorno (`.env`,
  `appsettings.Development.json` están en `.gitignore` a propósito). Si el
  secreto tiene un requisito de seguridad medible (ej. longitud mínima de
  clave HMAC), se valida en el arranque y la app falla rápido con un mensaje
  explícito si no lo cumple — no arranca en silencio con un valor débil. Ver
  `JwtSettingsValidator.EnsureValid`, invocado desde `Program.cs` antes de
  registrar la autenticación.

- **Composition root / capas.** No hay lógica de negocio en controllers ni
  acceso a `DbContext` fuera de `Toner.Infrastructure`/registro de DI. Un
  servicio nuevo de aplicación solo conoce `IApplicationDbContext`, nunca
  Npgsql ni EF Core directamente más allá de esa interfaz.

## Frontend (Vue 3)

- Lógica pesada (`filter`, `map`, `reduce` sobre listas, agrupaciones)
  siempre en `computed`, nunca inline en el `<template>` — se re-ejecuta en
  cada render si vive ahí.
- Instancias costosas de construir (`Intl.DateTimeFormat`, `Intl.NumberFormat`,
  etc.) a nivel de módulo, no dentro de un componente ni de una función que
  se llama por render.
- Toda carga de datos async lleva `catch` con logging real del error
  (`console.error` con el objeto, no solo un string), además del mensaje
  genérico que ve el usuario. Un `catch` que solo setea un flag de UI sin
  registrar el error en ningún lado no es manejo de errores.
- Imports específicos de librerías grandes (ej. iconos de Element Plus),
  nunca el paquete completo — afecta directamente el tamaño del bundle.
- Lógica de negocio repetida entre vistas (agrupaciones, `statusTagType()`,
  cascada departamento→ciudad, etc.) va a un composable en
  `src/composables/`, no copiada entre archivos. Si vas a tocar una vista
  que ya tiene esa lógica duplicada en otras, es buen momento para
  extraerla en vez de sumar una cuarta copia.

## Testing y verificación

- Nada se marca "resuelto" solo porque compila o porque pasan los tests
  unitarios / con proveedor InMemory. Cambios que tocan RLS, migraciones, o
  cualquier interacción real con Postgres (incluida la que dependa de
  `current_setting`, roles de conexión, o índices) se verifican con Docker
  real (`docker-compose.yml`) antes de darse por buenos. Los tests con
  InMemory no ejecutan RLS — un test en verde ahí no dice nada sobre si la
  política aísla correctamente (ver hallazgo N6, V2).
- Un test de seguridad/integridad debe demostrarse que detecta la
  regresión: hacer fallar el código a propósito (revertir el fix, comentar
  la política) y confirmar que el test se pone rojo antes de darlo por
  válido — no basta con que pase en verde contra el código ya corregido.
- Un commit por hallazgo o por feature lógica. No mezclar cambios no
  relacionados en el mismo commit (el historial del proyecto sigue este
  patrón consistentemente — úsalo como referencia de granularidad).

## Documentación viva

- `SECURITY_AUDIT.md`, `SECURITY_AUDIT_V2.md` y `CODE_QUALITY_AUDIT.md`
  reflejan el estado real del proyecto, no una foto del día que se
  escribieron. Cualquier hallazgo nuevo que surja trabajando en una
  funcionalidad (un problema de rendimiento, un hueco de RLS, una
  validación faltante) se agrega ahí — con el mismo formato de severidad /
  esfuerzo que ya usan — en vez de quedarse solo mencionado en el chat.
- `SECURITY_AUDIT.md` y `SECURITY_AUDIT_V2.md` están excluidos del repo
  (`.gitignore`) porque documentaron credenciales que estuvieron expuestas
  en el historial; siguen siendo la fuente de verdad local, solo no se
  versionan. `CODE_QUALITY_AUDIT.md` sí está versionado.
