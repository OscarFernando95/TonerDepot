# Auditoría de calidad de código — Toner

**Fecha:** 2026-08-17
**Alcance:** `backend/` (.NET 8 / EF Core 8 / PostgreSQL) + `frontend-web/` (Vue 3 / Vite / Element Plus / Pinia)
**Modo:** solo lectura. No se modificó ningún archivo.
**Base analizada:** commit `8c3b28f`, ~29.100 líneas de C# (≈15.800 excluyendo migraciones generadas) y ~8.100 líneas de frontend.

Este informe es complementario a `SECURITY_AUDIT.md` y `SECURITY_AUDIT_V2.md`; solo toca seguridad donde se cruza con rendimiento o diseño (RLS, `SecurityStampValidator`, paginación).

**Nota previa sobre calidad general:** el proyecto está por encima del promedio en varias dimensiones que suelen fallar — capas correctamente invertidas, proyecciones `.Select()` en todos los caminos de lectura, cero `SELECT *`, cero `try/catch` vacíos en backend, constantes con nombre en vez de números mágicos en la lógica de negocio, y comentarios que explican el *por qué* con referencias cruzadas a las auditorías. Los hallazgos de abajo son en su mayoría problemas de escala (lo que se rompe cuando las tablas crezcan), no de corrección.

---

## 1. Arquitectura general

### 1.1 Estructura de capas — correcta

El backend implementa Clean Architecture / Onion de cuatro capas con las dependencias bien dirigidas:

```
Toner.Domain          (sin dependencias — entidades, enums, constantes)
    ↑
Toner.Application     (servicios, DTOs, validators, IApplicationDbContext)
    ↑                                        ↑
Toner.Infrastructure  (EF Core, Npgsql, BCrypt, JWT, Hangfire)
    ↑                                        ↑
Toner.Api             (controllers, middleware, composition root)
```

Verificado en los `.csproj`: `Domain` no referencia nada, `Application` solo referencia `Domain`, `Infrastructure` referencia ambas, y `Api` referencia `Application` + `Infrastructure` (esto último es correcto: es el composition root, necesita ambas para el registro de DI).

**No hay lógica de negocio en controllers.** Los 19 controllers son delgados: validan con FluentValidation, delegan al servicio, devuelven `Ok(...)`. No hay un solo acceso a `DbContext` desde `Toner.Api` fuera del registro de DI. Esto es correcto y consistente.

**No hay acceso directo a BD desde capas altas.** `Toner.Application` solo conoce `IApplicationDbContext`, definido en la propia capa Application.

### 1.2 Violaciones y acoplamientos a corregir

**a) `IApplicationDbContext` expone `DbSet<T>` — abstracción con fuga (aceptable, pero hay que nombrarla)**

`src/Toner.Application/Common/Interfaces/IApplicationDbContext.cs` devuelve `DbSet<T>`, lo que obliga a `Toner.Application.csproj` a referenciar el paquete `Microsoft.EntityFrameworkCore`. Es decir: la capa de aplicación *sí* depende de EF Core, aunque no de Npgsql.

Es un compromiso deliberado y muy extendido (el propio template de Jason Taylor lo hace) y no recomiendo cambiarlo — el patrón Repository encima de EF Core suele ser peor que la enfermedad. Pero conviene documentarlo como decisión consciente, porque hoy no lo está: alguien puede leer "Clean Architecture" y asumir que se puede cambiar de ORM.

**b) `IAssetService.PrepareStatusChangeAsync` devuelve una entidad de dominio *trackeada* — acoplamiento implícito por unidad de trabajo**

[AssetService.cs:184](backend/src/Toner.Application/Assets/AssetService.cs#L184) devuelve `Task<Asset>` sin guardar, para que [ContractAssetService.AddAsync](backend/src/Toner.Application/Contracts/ContractAssetService.cs#L45) persista los cambios en su propio `SaveChangesAsync`. Funciona, y el comentario explica bien la intención (una sola transacción implícita).

El problema es que la corrección depende de una precondición invisible en la firma: **ambos servicios deben compartir la misma instancia scoped de `DbContext`**. Hoy es cierto por el registro de DI. Si alguien registra `IAssetService` como singleton, o lo llama desde un job con otro scope, el método compila, corre, y no persiste nada — en silencio. Un `IUnitOfWork` explícito (ver §8.4) haría la dependencia visible en el tipo.

**c) `TechnicianCheckInService` es un god-service**

[TechnicianCheckInService.cs](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs) inyecta **seis** dependencias (`IApplicationDbContext` + 5 servicios/motores) y su `CheckOutAsync` tiene ~180 líneas con ~25 ramas: valida check-out de instalación, de orden, y de ticket; registra lecturas de contador; upsertea cronogramas; escribe datos de activo externo; y delega el cierre a otros dos servicios. Es la única clase del backend que viola SRP de forma clara.

Descomposición natural: tres *handlers* (instalación / orden / ticket) detrás de una interfaz común, con el servicio quedándose solo con lo compartido (cerrar el `TimeLog`, cambiar el estado del técnico, validar el contador).

**d) `AssetService` tiene cuatro responsabilidades en 529 líneas**

[AssetService.cs](backend/src/Toner.Application/Assets/AssetService.cs) mezcla: CRUD de activos, la máquina de estados del ciclo de vida (`AllowedTransitions` + `ApplyStatusChange` + efectos colaterales sobre `ContractAssets` y `MaintenanceSchedules`), lecturas de contador (`AddMeterReadingAsync`, `GetMeterReadingsAsync`), y dos listados especializados para otros módulos (`ListPendingInstallationsAsync` para el portal del técnico, `ListForMeterReadingAsync` para el módulo de lecturas).

La máquina de estados en particular merece salir a `Toner.Domain` — hoy una regla de dominio pura (`AllowedTransitions`) vive en la capa de aplicación.

**e) `Program.cs` (344 líneas) es un composition root sin estructura**

Registro de 22 servicios, configuración de JWT, rate limiting, CORS, forwarded headers, HSTS, Swagger, Hangfire y el pipeline, todo lineal en un solo archivo. El patrón convencional (`services.AddApplication()`, `services.AddInfrastructure(config)`, `services.AddApiSecurity(config)` como métodos de extensión por capa) reduciría esto a ~40 líneas y pondría cada registro en la capa a la que pertenece.

**f) Duplicación de la extracción de claims en 5 controllers**

La propiedad `private RequestingUser CurrentUser` está copiada **literalmente** en `AssetsController`, `ContractsController`, `MeterReadingsController`, `MaintenanceOrdersController` y `ServiceTicketsController`. Son 5 copias de 10 líneas cada una que parsean los mismos claims con los mismos `Guid.Parse(...)!`.

Debería ser un método de extensión sobre `ClaimsPrincipal`, un `ControllerBase` propio, o un `ICurrentUserAccessor` scoped. Además, la duplicación amplifica el problema de §7.3: cada copia lanza `FormatException` → 500 si falta un claim.

### 1.3 Frontend — la estructura escala, pero faltan dos carpetas

No es un `src/components/` plano. La organización es por feature y es correcta:

```
src/
  api/          20 módulos, uno por recurso + http.ts + types.ts
  views/        agrupadas por dominio: assets/ clients/ contracts/
                maintenance/ portal/ technicians/ tickets/ users/ auth/
  layouts/      AppLayout.vue
  components/   CreateClientDialog.vue + board/{FlapText,LaneStatus}.vue
  stores/       auth.ts
  router/       index.ts + meta.d.ts
```

**Lo que falta y ya duele:**

- **No existe `src/composables/`.** La lógica de negocio vive dentro de los componentes. El propio código lo documenta: [CreateClientDialog.vue:40](frontend-web/src/components/CreateClientDialog.vue#L40) dice *"Mismo patrón (repetido, sin composable) que se usa en UsersView.vue y ClientDetailView.vue"* — `departmentsFor()` / `citiesInDepartment()` está copiado en 3 archivos. Peor: la lógica de agrupación tri-nivel `groupedByCity` (~40 líneas) está copiada casi idéntica en `AssetsListView.vue`, `MaintenanceSchedulesView.vue` y `MeterReadingsView.vue`. Y `statusTagType()` aparece en 6+ vistas con ramas ligeramente distintas.
- **`components/` está casi vacío (3 archivos) frente a 24 vistas de 100–610 líneas.** No hay componentes de tabla/filtro/diálogo reutilizables; cada vista construye su propia tabla desde cero. Es la causa directa de que `AssetsListView.vue` tenga 559 líneas y `MyWorkView.vue` 610.
- **Un solo store de Pinia (`auth`).** Ver §6.5 — el problema no es exceso de estado global, es defecto: catálogos compartidos (ciudades, marcas, clientes) se re-descargan en cada vista.

---

## 2. Rendimiento — Backend

### 2.1 El costo acumulado de RLS + SecurityStamp (análisis solicitado)

**Cómo funciona realmente el sobrecosto.** `TenantContextInterceptor` es un `DbConnectionInterceptor` que engancha `ConnectionOpenedAsync`. Esto **no** se dispara una vez por request: se dispara **cada vez que EF Core toma una conexión del pool**. Y EF Core abre y cierra la conexión *por consulta* cuando no hay una transacción explícita ni una conexión abierta manualmente — que es exactamente el caso de este proyecto (el comentario de [Program.cs:141](backend/src/Toner.Api/Program.cs#L141) confirma: *"sin transacciones explícitas en todo el código (verificado)"*).

Es decir: **cada consulta paga un round-trip extra** de `SELECT set_config(...), set_config(...)`. No es un costo de conexión TCP (el pool de Npgsql evita eso), es un round-trip de red + parse/plan en Postgres.

El `SELECT` del `SecurityStamp` en `SecurityStampValidator` es distinto: es **uno por request autenticado**, indexado por PK, y su costo real es 2 round-trips (el `set_config` de su propia conexión + el `SELECT`). La evaluación documentada en [Program.cs:205-209](backend/src/Toner.Api/Program.cs#L205) es correcta y no la contradigo: cachearlo introduciría staleness a cambio de poco.

**El problema no es el costo fijo por request. Es el multiplicador por consulta.** Cuantificación sobre los endpoints reales:

| Endpoint | Consultas | Round-trips con interceptor | Sobrecosto |
|---|---|---|---|
| `GET /api/cities` | 1 stamp + 1 lista | 4 | +100 % |
| `GET /api/assets` (staff) | 1 stamp + 1 lista + 1 lecturas | 6 | +100 % |
| `GET /api/dashboard/summary` | 1 stamp + 4 agregados | 10 | +100 % |
| `POST /api/assets/{id}/meter-readings` | 1 stamp + ~11 (exists, última lectura, engine: schedule + orden abierta, save, DTO, motor de asignación: orden + ciudad + candidatos + 2 cargas + save) | ~24 | +100 % |
| `MaintenanceScheduleEvaluationJob` con 1.000 cronogramas | ~3.000 | ~6.000 | +100 % |

Con un RTT de 0,3 ms (misma AZ) el job pasa de ~0,9 s a ~1,8 s de puro tiempo de red — irrelevante. Con 2 ms (cross-AZ o Postgres gestionado con proxy delante) pasa de ~6 s a ~12 s, y el `POST` de lectura de contador pasa de ~24 ms a ~48 ms de red. **El sobrecosto es proporcional, no absoluto: duplica lo que ya hay.** Por eso la palanca real no es optimizar el `set_config`, es reducir el número de consultas (§2.2–2.5) — cada consulta eliminada quita dos round-trips, no uno.

**Cómo reducir las aperturas de conexión sin debilitar RLS.**

La opción limpia: **abrir la conexión una vez por request y mantenerla abierta**. Un segundo middleware, colocado *después* de `UseAuthentication()`, que haga `await db.Database.OpenConnectionAsync()` y la cierre al terminar. Con eso `ConnectionOpenedAsync` se dispara **una vez por request** en vez de una vez por consulta, y todas las consultas posteriores reutilizan la misma sesión con las variables ya seteadas.

Por qué no debilita RLS:
- Las variables se siguen seteando antes de cualquier consulta de negocio.
- Npgsql sigue reseteando el estado de la sesión al devolver la conexión al pool (la garantía en la que ya se apoya todo el diseño, verificada por `NpgsqlSessionResetTests`).
- El contexto de tenencia de una request no cambia a mitad de request, así que no hay escenario donde haga falta re-setear.

Restricción importante de ubicación: el diseño actual evalúa el `TenantContext` de forma perezosa (`Push(() => FromPrincipal(context.User))`) precisamente para que la consulta temprana del `SecurityStampValidator` vea `Anonymous`. Si se abriera la conexión *antes* de `UseAuthentication()`, quedaría fijada como `Anonymous` para toda la request y las políticas denegarían todo. **El middleware debe ir después de `UseAuthentication()`**, y la consulta del stamp seguiría usando su propia conexión efímera (2 round-trips, como hoy).

Costo: cada request retiene una conexión del pool durante toda su vida, en vez de solo durante cada consulta. Solo importa si la concurrencia máxima supera el tamaño del pool (por defecto 100 en Npgsql). Para el perfil de esta aplicación — decenas de usuarios internos — no es un riesgo, pero conviene medirlo antes de subir a producción con carga real.

Alternativa complementaria (no excluyente): usar `SET LOCAL` dentro de transacciones explícitas para los flujos multi-paso que de todos modos deberían ser transaccionales (§8.4). Ahí el `SET LOCAL` sí funciona y desaparece la advertencia documentada en el interceptor.

Lo que **no** recomiendo: cachear el `SecurityStamp` (ya evaluado y descartado con buen criterio), ni relajar `FORCE ROW LEVEL SECURITY`, ni mover el `set_config` a un `SET` concatenado (reintroduciría inyección).

### 2.2 N+1 real en el job diario — el peor caso del backend

[MaintenanceScheduleEvaluationJob.cs:75-89](backend/src/Toner.Infrastructure/Jobs/MaintenanceScheduleEvaluationJob.cs#L75):

```csharp
foreach (var assetId in schedules)          // todos los cronogramas activos
{
    var lastReading = await _db.MeterReadings...FirstOrDefaultAsync();  // 1 query
    var order = await _engine.EvaluateAsync(assetId, ...);              // 2 queries dentro
    if (order is not null) { await _db.SaveChangesAsync(); }            // 1 query
}
foreach (var orderId in createdOrderIds)
{
    await _assignmentEngine.AssignMaintenanceOrderAsync(orderId, ...);  // ~5 queries dentro
}
```

`EvaluateAsync` hace internamente dos consultas más ([MaintenanceScheduleEngine.cs:85](backend/src/Toner.Application/Maintenance/MaintenanceScheduleEngine.cs#L85) carga el `MaintenanceSchedule` con `Include(Asset).ThenInclude(AssetModel)`, y [línea 93](backend/src/Toner.Application/Maintenance/MaintenanceScheduleEngine.cs#L93) verifica si hay orden abierta). Total: **3 consultas por activo, más ~5 por orden generada**.

Con 1.000 activos instalados: ~3.000 consultas, ~6.000 round-trips. Con 10.000: ~30.000 consultas. Es lineal pero con una constante altísima, y crece exactamente con el éxito del negocio.

Reescritura: una sola consulta que traiga `(AssetId, thresholds del AssetModel, campos del schedule, última lectura vía DISTINCT ON, existencia de orden abierta)` para todos los cronogramas activos, evaluar en memoria (la lógica de `EvaluateAsync` es aritmética pura una vez que tienes los datos), y un solo `SaveChangesAsync` con todas las órdenes. De ~3.000 consultas a ~3.

### 2.3 N+1 en `BackfillMissingAsync`

[MaintenanceScheduleService.cs:83-103](backend/src/Toner.Application/Maintenance/MaintenanceScheduleService.cs#L83): loop sobre los activos sin cronograma, con una consulta de `MeterReadings` por activo, más las 2 consultas que `UpsertForInstallationAsync` hace internamente ([línea 31](backend/src/Toner.Application/Maintenance/MaintenanceScheduleEngine.cs#L31) carga `Asset` + `AssetModel`, [línea 34](backend/src/Toner.Application/Maintenance/MaintenanceScheduleEngine.cs#L34) busca el schedule). **3 consultas por activo.**

Es un endpoint administrativo de uso puntual (`POST /api/maintenance-schedules/backfill`), así que el impacto operativo es menor que el del job — pero es el mismo patrón y se corrige igual.

### 2.4 Sobre-materialización: se traen TODAS las lecturas para calcular un máximo

Este es el hallazgo de rendimiento con mayor impacto en los endpoints de lectura, y está **duplicado en cuatro lugares**:

- [AssetService.AttachLastMeterReadingsAsync:107](backend/src/Toner.Application/Assets/AssetService.cs#L107) — usado por `GET /api/assets`
- [AssetService.ListForMeterReadingAsync:474](backend/src/Toner.Application/Assets/AssetService.cs#L474) — copia inline del mismo bloque
- [MaintenanceScheduleService.AttachLastKnownCountersAsync:121](backend/src/Toner.Application/Maintenance/MaintenanceScheduleService.cs#L121)
- [ContractAssetService.AttachAssetMetricsAsync:122](backend/src/Toner.Application/Contracts/ContractAssetService.cs#L122)

Todos hacen lo mismo:

```csharp
var readings = await _db.MeterReadings
    .Where(m => assetIds.Contains(m.AssetId))   // TODAS las lecturas de TODOS los activos
    .Select(m => new { m.AssetId, m.ReadingDate, m.CounterValue })
    .ToListAsync(cancellationToken);

var lastByAsset = readings.GroupBy(...).ToDictionary(g => g.Key, g => g.OrderByDescending(...).First()...);
```

`MeterReadings` es la tabla que más rápido crece del sistema: se inserta una fila en cada check-out de instalación, en cada check-out de ticket con contador, en cada orden de mantenimiento completada, y en cada lectura manual del módulo de contadores. Con 500 activos y 50 lecturas cada uno, `GET /api/assets` materializa **25.000 filas en memoria para producir 500 números**. Con 2 años de operación y lecturas mensuales por activo, son cientos de miles.

El comentario en [AssetService.cs:95](backend/src/Toner.Application/Assets/AssetService.cs#L95) justifica el diseño por paridad entre el proveedor InMemory de los tests y Npgsql. Ese es el problema de fondo: **el proveedor de tests está dictando la forma de las consultas de producción**. El repositorio ya tiene la infraestructura para probar contra Postgres real (`RlsFixture`, `PostgresFactAttribute` en `tests/Toner.Application.Tests/Rls/`); extenderla a estos tests permite usar `DISTINCT ON (AssetId) ... ORDER BY AssetId, ReadingDate DESC` o una función de ventana, que resuelve lo mismo en una pasada indexada sin traer nada de más.

`AttachAssetMetricsAsync` es un caso especial: sí necesita la primera y la última lectura (para el promedio mensual), pero eso son dos filas por activo, no todas — `DISTINCT ON` con dos órdenes, o un agregado `min/max` con `FILTER`.

### 2.5 Round-trip redundante después de cada escritura

El patrón `ToDtoAsync(id)` re-consulta la fila recién escrita al final de **cada** método de escritura. Está en `AssetService` (Create, Update, ChangeStatus — y ahí son **2** consultas extra: la proyección más `GetLastMeterReadingAsync`), `ServiceTicketService` (Create, Assign, Claim, SetStatus), `MaintenanceOrderService` (Assign, Claim, Complete, Cancel), `ContractService`, `ClientService`, `ClientLocationService`, `UserService`, `ContractAssetService`.

Son ~20 endpoints, cada uno pagando 1–2 consultas + sus round-trips de `set_config` para releer datos que la aplicación acaba de escribir. Es un compromiso legítimo (la proyección incluye campos calculados y joins que la entidad en memoria no tiene), pero en los casos donde el DTO se puede construir desde la entidad ya trackeada más lo poco que falte, la relectura sobra.

### 2.6 Consulta duplicada en `AssetService.GetByIdAsync`

[AssetService.cs:122-146](backend/src/Toner.Application/Assets/AssetService.cs#L122):

```csharp
var asset = await Projected(_db).FirstOrDefaultAsync(a => a.Id == id, ...);   // ya trae CurrentClientId
...
if (!requestingUser.IsStaff)
{
    var belongsToClient = await _db.Assets.AnyAsync(a => a.Id == id && ...);   // consulta redundante
}
```

El DTO proyectado ya contiene `CurrentClientId` ([línea 521](backend/src/Toner.Application/Assets/AssetService.cs#L521)). La verificación puede ser `asset.CurrentClientId != clientId` en memoria. Es una consulta + 2 round-trips por cada `GET /api/assets/{id}` del portal de clientes.

### 2.7 `.Include()` seguido de `.Select()` — no hace nada

EF Core **ignora los `Include` cuando la consulta termina en una proyección**. Los siguientes `Include` son código muerto que además induce a error al leer:

- [UserService.Projected:141](backend/src/Toner.Application/Users/UserService.cs#L141) — 3 `Include` ignorados
- [AssetService.GetStatusHistoryAsync:291](backend/src/Toner.Application/Assets/AssetService.cs#L291)
- [AssetService.AddMeterReadingAsync:351](backend/src/Toner.Application/Assets/AssetService.cs#L351) y [GetMeterReadingsAsync:373](backend/src/Toner.Application/Assets/AssetService.cs#L373)
- [ServiceTicketService.GetAssignmentHistoryAsync:234](backend/src/Toner.Application/Tickets/ServiceTicketService.cs#L234)
- [MaintenanceOrderService.GetAssignmentHistoryAsync:223](backend/src/Toner.Application/Maintenance/MaintenanceOrderService.cs#L223)
- [TechnicianService.ListCoverageAsync:42](backend/src/Toner.Application/Technicians/TechnicianService.cs#L42) y [AddCoverageAsync:70](backend/src/Toner.Application/Technicians/TechnicianService.cs#L70)

No degradan el rendimiento (EF los descarta al construir el árbol), pero sí la legibilidad. Los `Include` de `AuthService` ([línea 46](backend/src/Toner.Application/Auth/AuthService.cs#L46), [línea 137](backend/src/Toner.Application/Auth/AuthService.cs#L137)) **sí** son necesarios: ahí no hay `Select`, se materializa la entidad.

### 2.8 Async, `Task.WhenAll` e `IAsyncEnumerable`

**Operaciones síncronas bloqueantes: ninguna.** No hay `.Result`, `.Wait()`, `GetAwaiter().GetResult()` ni I/O síncrona en toda la capa de aplicación. El `ConnectionOpened` síncrono del interceptor es el override obligatorio de la clase base y solo se invoca en caminos síncronos que el código no usa. Esto está bien.

**`Task.WhenAll`: no se usa en ningún lado, y en la mayoría de los casos es correcto que así sea** — un `DbContext` de EF Core no es thread-safe, así que paralelizar consultas sobre la instancia scoped compartida lanzaría `InvalidOperationException`. No es un descuido.

Donde **sí** aplicaría, porque el proyecto ya tiene la pieza necesaria: `IDbContextFactory<TonerDbContext>` está registrado ([Program.cs:133](backend/src/Toner.Api/Program.cs#L133)). `DashboardService.GetSummaryAsync` hace **4 consultas independientes en secuencia** ([DashboardService.cs:47-65](backend/src/Toner.Application/Dashboard/DashboardService.cs#L47)) que no comparten estado ni dependen entre sí. Con 4 contextos de la factory podrían correr concurrentes, dividiendo la latencia del endpoint más pesado del sistema por ~4. Contrapartida: 4 conexiones simultáneas del pool y 4 `set_config`, así que el ahorro es de latencia, no de trabajo total. Vale la pena solo si el dashboard se percibe lento; medir primero.

**`IAsyncEnumerable`: no se usa y no hace falta hoy.** El único candidato real sería el job de §2.2, y ahí la solución correcta es eliminar el loop, no hacerlo streaming.

---

## 3. Base de datos — índices y queries

### 3.1 Índices de soporte de RLS — la premisa hay que matizarla

**Los cuatro índices que las políticas RLS necesitan existen todos.** Verificado contra `TonerDbContextModelSnapshot.cs`:

| Columna en la política | Índice | Estado |
|---|---|---|
| `ClientLocations.ClientId` | `IX_ClientLocations_ClientId` | ✅ existe |
| `ClientLocations.Id` (lado interno de los EXISTS) | PK | ✅ |
| `ServiceTickets.ClientLocationId` | `IX_ServiceTickets_ClientLocationId` | ✅ existe |
| `Assets.CurrentClientLocationId` | `IX_Assets_CurrentClientLocationId` | ✅ existe |
| `Contracts.ClientId` | `IX_Contracts_ClientId` | ✅ existe |

Todos vienen de la convención de EF Core (índice automático por FK). No hay ninguna política RLS sin índice de respaldo por *ausencia* de índice.

### 3.2 Pero hay un problema real: el cast `::text` inutiliza esos índices

Las cuatro políticas comparan así:

```sql
"ClientId"::text = current_setting('app.current_client_id', true)
```

**Postgres no puede usar un índice btree sobre `ClientId` (tipo `uuid`) para satisfacer un predicado sobre `ClientId::text`.** La expresión indexada es `ClientId`, el predicado es sobre `ClientId::text` — no coinciden. El planner degrada a **seq scan con filtro**, y ese es exactamente el "full scan silencioso" que se buscaba evitar.

Impacto por tabla:

- **`ClientLocations` y `Contracts`** (políticas directas): el predicado se evalúa fila por fila sobre la tabla completa en cada consulta bajo un token de Cliente. Hoy son tablas chicas, así que no se nota; el problema es que es invisible y no escala.
- **`ServiceTickets` y `Assets`** (políticas con `EXISTS`): aquí el impacto es menor. El `EXISTS` interno busca por `cl."Id" = ...`, que es PK y sí usa índice; el cast `::text` se aplica sobre la única fila ya recuperada. El costo es una sonda a la PK de `ClientLocations` por cada fila candidata de la tabla externa — barato por fila, pero pagado N veces donde N es la tabla entera (ver §4).

**Corrección recomendada** — castear el *setting*, no la columna:

```sql
"ClientId" = NULLIF(current_setting('app.current_client_id', true), '')::uuid
```

Esto **mantiene el comportamiento fail-closed**: `NULLIF` convierte la cadena vacía en `NULL`, y `ClientId = NULL` da `NULL` → la fila no pasa. Si la variable no está seteada, `current_setting(..., true)` devuelve `NULL` y el resultado es igual. La única diferencia frente a hoy es que un valor no-UUID en la variable lanzaría error de cast en vez de simplemente no matchear — lo cual es preferible (falla ruidosamente, coherente con el criterio del propio interceptor).

Alternativa si se prefiere no tocar las políticas: un índice de expresión `CREATE INDEX ... ON "ClientLocations" (("ClientId"::text))`. Funciona, pero duplica índices y hay que replicarlo en `Contracts`.

**⚠️ Actualización tras verificar contra Postgres real (migración `FixRlsCastForIndexUsage`, aplicada y medida con `EXPLAIN ANALYZE` sobre 8.000 filas sintéticas): este cast es necesario pero NO suficiente.** La caracterización original de esta sección ("esto es sargable, el planner puede usar el índice") estaba incompleta.

Verificado empíricamente:

```
-- Predicado de ClientId aislado (sin el OR de is_staff) — SÍ usa el índice:
Index Scan using "IX_ClientLocations_ClientId" (cost=0.28..8.30) (actual rows=1)
  Index Cond: ("ClientId" = '...'::uuid)

-- La política real, con el cast ya corregido — SIGUE en Seq Scan:
Seq Scan on "ClientLocations" (cost=0.00..352.06 rows=41) (actual rows=1)
  Filter: (is_staff = 'on' OR ("ClientId" = NULLIF(current_setting(...), '')::uuid))
  Rows Removed by Filter: 8001
```

La causa: `current_setting(...)` es una función `STABLE`, no `IMMUTABLE` — Postgres no la resuelve en tiempo de planificación. Un `OR` entre "algo que solo se conoce en ejecución" (el bypass de staff) y "una condición indexable" (el `ClientId`) no genera un plan condicional; el planner cae a Seq Scan completo sin importar cómo esté escrito el lado indexable. Se confirmó que no es un problema de sintaxis probando con dos políticas `PERMISSIVE` separadas (`is_staff` por un lado, `ClientId` por otro) en vez de un solo `OR` explícito: mismo plan — Postgres combina múltiples políticas permisivas con `OR` internamente, de forma equivalente. Es una limitación estructural de RLS en Postgres con el patrón "bypass OR condición", no algo que se arregle reformulando el predicado. `service_tickets_client_isolation` y `assets_client_isolation` tienen la misma forma (`is_staff OR EXISTS(...)`) y por lo tanto el mismo límite (inferido de la misma causa, no verificado por separado).

El cast sigue aplicado porque es una mejora real y sin downside (deja la columna lista para cualquier solución futura, mismo comportamiento fail-closed, suite de RLS en verde), pero **no cierra el hallazgo #2**: las cuatro políticas siguen haciendo seq scan completo bajo un token de Cliente. La solución real requiere eliminar el `OR` de la ruta caliente.

**Resuelto parcialmente en la migración `SplitRlsPoliciesByRole` (commit siguiente).** Dos correcciones al análisis de arriba, ambas medidas:

1. **`BYPASSRLS` no hacía falta.** Basta partir cada política en dos restringidas por rol (`TO toner_app` / `TO toner_app_staff`): Postgres descarta al planificar la que no aplica al rol activo, dejando al Cliente un predicado limpio. Y la premisa de que `current_setting()` no podía indexarse **era falsa** — las funciones `STABLE` sí pueden ser clave de índice; el único bloqueo era el `OR`.
2. **El arreglo funciona en 2 de las 4 tablas, no en las 4.** Medido con `EXPLAIN ANALYZE`:

| Tabla | Antes | Después |
|---|---|---|
| `ClientLocations` | `Seq Scan` `0.00..352.06` | ✅ `Index Scan` `0.29..8.31` |
| `Contracts` | `Seq Scan` | ✅ `Index Scan` `0.29..8.31` |
| `ServiceTickets` | `Seq Scan` `0.00..33370.33`, `hashed SubPlan` | ❌ `Seq Scan` `0.00..33350.32`, `hashed SubPlan` — sin cambio real |
| `Assets` | (misma forma) | ❌ `Seq Scan` — sin cambio real |

Las dos tablas con política `EXISTS` **no mejoran**: Postgres trata los predicados de RLS como *security barrier* y no los sube a un join contra la tabla externa, así que el escaneo completo del lado externo se paga igual (el subplan interno ya se evaluaba una sola vez, hasheado, antes y después). Cerrar esas dos exige la **fase 3** que la migración original de RLS dejó diferida: denormalizar `ClientId` en `ServiceTickets` y `Assets` para que sus políticas sean directas y sargables como las otras dos.

### 3.3 Índices faltantes, por consulta real

| Consulta (dónde) | Índice recomendado | Por qué |
|---|---|---|
| "Última lectura por activo" — **5 sitios**: [AssetService:107](backend/src/Toner.Application/Assets/AssetService.cs#L107), [:320](backend/src/Toner.Application/Assets/AssetService.cs#L320), [:500](backend/src/Toner.Application/Assets/AssetService.cs#L500), [MaintenanceOrderService:165](backend/src/Toner.Application/Maintenance/MaintenanceOrderService.cs#L165), [TechnicianCheckInService:208](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L208), [job:77](backend/src/Toner.Infrastructure/Jobs/MaintenanceScheduleEvaluationJob.cs#L77) | `MeterReadings (AssetId, ReadingDate DESC)` | Hoy solo hay `(AssetId)`, así que cada búsqueda hace index scan + **sort**. Es la consulta más repetida del sistema. **El índice de mayor impacto de toda la lista.** |
| `ORDER BY CreatedAt DESC` en todos los listados de tickets ([ServiceTicketService:95](backend/src/Toner.Application/Tickets/ServiceTicketService.cs#L95), [:120](backend/src/Toner.Application/Tickets/ServiceTicketService.cs#L120)) | `ServiceTickets (CreatedAt DESC)` | Sin él, cada listado ordena la tabla completa en memoria. Además es el índice que habilita paginación por cursor (§4). |
| Ídem órdenes ([MaintenanceOrderService:38](backend/src/Toner.Application/Maintenance/MaintenanceOrderService.cs#L38), [:45](backend/src/Toner.Application/Maintenance/MaintenanceOrderService.cs#L45), [:72](backend/src/Toner.Application/Maintenance/MaintenanceOrderService.cs#L72)) | `MaintenanceOrders (CreatedAt DESC)` | Igual |
| Dashboard: tickets resueltos en el período ([DashboardService:47](backend/src/Toner.Application/Dashboard/DashboardService.cs#L47)) | `ServiceTickets (ResolvedAt) WHERE "ResolvedAt" IS NOT NULL` | Índice parcial: solo indexa las filas relevantes |
| Dashboard: tickets abiertos ([:57](backend/src/Toner.Application/Dashboard/DashboardService.cs#L57)) y filtro de estados activos en coverage | `ServiceTickets (Status)` | Baja cardinalidad (7 valores) pero muy selectivo para "Abierto/SinAsignar" cuando la mayoría estén cerrados |
| Dashboard: órdenes completadas ([:52](backend/src/Toner.Application/Dashboard/DashboardService.cs#L52)) | `MaintenanceOrders (CompletedAt) WHERE "CompletedAt" IS NOT NULL` | Ídem |
| Dashboard: time logs del período ([:62](backend/src/Toner.Application/Dashboard/DashboardService.cs#L62)) | `TimeLogs (StartTime)` | Rango temporal sin índice |
| Filtro por `LifecycleStatus` ([AssetService:400](backend/src/Toner.Application/Assets/AssetService.cs#L400), [:455](backend/src/Toner.Application/Assets/AssetService.cs#L455), [MaintenanceScheduleService:73](backend/src/Toner.Application/Maintenance/MaintenanceScheduleService.cs#L73)) | `Assets (LifecycleStatus)` | 3 sitios, incluido `GET /api/meter-readings` |
| "TimeLog abierto" ([TechnicianCheckInService:46](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L46), [:118](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L118), [:158](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L158), [AssetService:433](backend/src/Toner.Application/Assets/AssetService.cs#L433)) | `TimeLogs (TechnicianId) WHERE "EndTime" IS NULL` y `TimeLogs (AssetId) WHERE "EndTime" IS NULL` | Índices parciales diminutos (a lo sumo un puñado de filas abiertas a la vez) que convierten estas 4 consultas en lookups de una página |
| Cronogramas activos (job) | `MaintenanceSchedules (AssetId) WHERE "IsActive"` | Solo si `IsActive = false` llega a ser la mayoría |

**Ya existe y está bien resuelto:** el índice único **parcial** `ContractAssets (AssetId) WHERE "EndDate" IS NULL` ([ContractAssetConfiguration.cs:30](backend/src/Toner.Infrastructure/Persistence/Configurations/ContractAssetConfiguration.cs#L30)) — cubre las consultas de "vínculo de contrato activo" *y* garantiza la invariante a nivel de BD contra condiciones de carrera. Es el mejor índice del esquema.

### 3.4 Índices redundantes o no usados

- **Tabla `Evidence` completa: 3 índices sobre una tabla que ningún código toca.** La entidad está mapeada, la tabla se crea con FKs e índices sobre `MaintenanceOrderId`, `ServiceTicketId` y `UploadedByUserId`, y `TonerDbContext` expone `DbSet<Evidence>`. Pero **no está en `IApplicationDbContext`** y no hay ni un servicio, controller o consulta que la use. Es esquema muerto. No cuesta rendimiento (nunca se escribe), pero es superficie de mantenimiento y confunde el modelo.
- **Índices de FK "quién hizo qué" nunca consultados por esa columna:** `AssetStatusLogs.ChangedByUserId`, `MeterReadings.RegisteredByUserId`, `AssignmentHistories.AssignedByUserId`, `Users.CityId`, `Users.RoleId`. Todas estas columnas se usan solo para *join hacia* `Users`/`Cities` (donde el índice útil es la PK del otro lado), nunca como predicado de filtrado. Son índices que se mantienen en cada `INSERT` sin beneficio. Impacto bajo — pero en `MeterReadings`, que es la tabla de mayor volumen de escritura, no es cero.
- **No hay índices duplicados ni solapados.** `TechnicianCoverages` tiene `(CityId)` y `(TechnicianId, CityId)` único, que parecen solapados pero no lo son: `(CityId)` sirve la búsqueda inversa de `AssignmentEngine.FindCandidateAsync` ([AssignmentEngine.cs:105](backend/src/Toner.Application/Assignment/AssignmentEngine.cs#L105)), que el compuesto no puede satisfacer (`CityId` no es la primera columna). Correcto como está.

### 3.5 Migraciones de EF Core

**Correcto:**
- Todas las `DateTime` son `timestamp with time zone` (verificado en `InitialCreate`), coherente con el uso de `DateTime.UtcNow` en todo el código y los conversores `UtcDateTimeConverter` en la serialización.
- Dinero como `decimal(18,4)` explícito ([ContractConfiguration.cs](backend/src/Toner.Infrastructure/Persistence/Configurations/ContractConfiguration.cs)), no `float`.
- Enums persistidos como `string` con `HasMaxLength(30)` — legible en la BD y estable ante reordenamientos del enum.
- `OnDelete(DeleteBehavior.Restrict)` en todas las relaciones — conservador y correcto para un sistema con trazabilidad.
- `HasMaxLength` en todas las columnas de texto; ninguna `text` sin límite.
- Las migraciones de RLS documentan la trampa de `FORCE ROW LEVEL SECURITY` con migraciones futuras y remiten al README. Muy buena práctica.

**A revisar:**

**a) PKs `uuid` v4 aleatorias — fragmentación de índices en las tablas de más escritura**

[BaseEntity.cs:5](backend/src/Toner.Domain/Common/BaseEntity.cs#L5): `Guid Id { get; set; } = Guid.NewGuid();` con `ValueGeneratedNever()`. La generación en cliente es un requisito real y bien documentado ([EntityTypeBuilderExtensions.cs](backend/src/Toner.Infrastructure/Persistence/Configurations/EntityTypeBuilderExtensions.cs): sincronización offline desde Flutter).

El problema no es generar en cliente, es que `Guid.NewGuid()` produce UUIDv4 **aleatorio**. Cada `INSERT` cae en una página arbitraria del btree de la PK → page splits constantes, índice inflado, peor localidad de caché. En `MeterReadings`, `TimeLogs`, `AssetStatusLogs` y `AssignmentHistories` (todas append-only y de alto volumen) esto se acumula.

Solución sin perder la generación offline: **UUIDv7** (ordenado por tiempo). Mantiene unicidad global y generación en cliente, pero los valores consecutivos quedan próximos en el índice. En .NET 8 requiere una implementación propia o un paquete; en .NET 9+ es `Guid.CreateVersion7()`. Cambio de una línea en `BaseEntity`, sin migración de datos (los IDs viejos siguen siendo válidos).

**b) Cero constraints `CHECK` — todas las invariantes viven solo en C#**

No hay una sola constraint de dominio en la BD: nada impide `CounterValue < 0`, `EndDate < StartDate` en `Contract`/`ContractAsset`, `LifecycleStatus` con un string que no corresponde a ningún valor del enum, o `MeterReading` con `ReadingDate` en el futuro. Todas esas reglas están correctamente implementadas en los servicios, pero la BD no las respalda.

Es un tema de defensa en profundidad con la misma lógica que motivó RLS: si la aplicación tiene un bug o alguien escribe por fuera de EF (script de ops, migración de datos), el dato se corrompe en silencio. Las de mayor valor y menor costo: `CHECK ("CounterValue" >= 0)` en `MeterReadings`, `CHECK ("EndDate" IS NULL OR "EndDate" >= "StartDate")` en `Contracts` y `ContractAssets`, y `CHECK ("LifecycleStatus" IN (...))` en `Assets` (o directamente un tipo `ENUM` de Postgres).

**c) `AssetModel` guarda los umbrales sin validación de rango**

`GeneralPrintThreshold`, `UnitsPrintThreshold`, `ConsumablesPrintThreshold`, `GeneralMonthsInterval`, `UnitsMonthsInterval` son enteros sin `CHECK (> 0)`. Un `0` en `ConsumablesPrintThreshold` haría que `MaintenanceScheduleEngine` genere una orden de mantenimiento en cada evaluación, para siempre. El validator del frontend pone `:min="1"` y `CreateAssetModelRequestValidator` valida, pero la BD acepta cualquier cosa.

### 3.6 Proyección de columnas — bien resuelto

**No hay un solo `SELECT *` en un camino de lectura.** Todos los listados y `GetById` usan `.Select()` a un DTO con las columnas exactas. Esto es notablemente disciplinado y hay que decirlo.

Las excepciones son escrituras donde se carga la entidad completa para mutarla (correcto por definición). Los tres casos donde se carga de más sin necesitarlo:

- Se carga la entidad `MeterReading` completa cuando solo se lee `CounterValue`: [AssetService:320](backend/src/Toner.Application/Assets/AssetService.cs#L320), [MaintenanceOrderService:165](backend/src/Toner.Application/Maintenance/MaintenanceOrderService.cs#L165), [TechnicianCheckInService:208](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L208). Contraste: [AssetService.GetLastMeterReadingAsync:499](backend/src/Toner.Application/Assets/AssetService.cs#L499) sí proyecta solo el contador — el patrón correcto ya existe en el mismo archivo, solo falta aplicarlo.
- [MaintenanceScheduleService.AttachMaintenanceSummariesAsync:149](backend/src/Toner.Application/Maintenance/MaintenanceScheduleService.cs#L149) **re-consulta los mismos cronogramas** que `Projected()` acaba de traer, ahora con el grafo `Asset` → `AssetModel` completo, solo para leer 5 umbrales enteros. Si el DTO llevara esos 5 campos, la segunda consulta desaparece por completo.
- [MaintenanceScheduleEngine.EvaluateAsync:85](backend/src/Toner.Application/Maintenance/MaintenanceScheduleEngine.cs#L85) carga `Asset` + `AssetModel` enteros para leer los mismos 5 umbrales. Aquí el `Schedule` sí se necesita trackeado (se muta), pero `Asset`/`AssetModel` no.

### 3.7 Full scans conceptuales

Con los índices actuales, estas consultas escanean la tabla completa:

1. **Cualquier listado bajo un token de Cliente sobre `ClientLocations` o `Contracts`** — por §3.2 (cast `::text`).
2. **`GET /api/tickets`** — sin índice en `CreatedAt`, ordena la tabla entera; sin paginación, la devuelve entera.
3. **`GET /api/assets`** con filtro por `LifecycleStatus` — sin índice, filtra fila por fila.
4. **Las 4 consultas del dashboard** — sin índices en `ResolvedAt`/`CompletedAt`/`StartTime`/`Status`, cada una escanea su tabla completa. Con el período de 90 días sobre un histórico de años, se descarta la mayoría de lo escaneado.
5. **"Última lectura por activo"** — index scan por `AssetId` seguido de sort, repetido hasta 1.000 veces en el job.

---

## 4. Paginación (hallazgo #12, sigue abierto)

### 4.1 Alcance real — confirmado y ampliado

```
grep -rn "\.Skip(\|\.Take(" backend/src backend/tests   →  0 coincidencias
grep -rn "el-pagination\|page-size\|pageSize" frontend-web/src  →  0 coincidencias
```

**Confirmado: cero `Skip`/`Take` en toda la capa `Toner.Application`.** Y el alcance es peor de lo que registra el hallazgo #12: tampoco hay paginación en tests, ni componente de paginación en el frontend, ni ningún parámetro de consulta de límite en ningún controller. Es **ausencia total de extremo a extremo**: no hay ni siquiera un tope de seguridad.

Inventario completo de endpoints de listado sin límite (17):

| Endpoint | Tabla base | Crece con | RLS |
|---|---|---|---|
| `GET /api/assets` | `Assets` + todas sus `MeterReadings` | flota instalada × tiempo | ✅ `EXISTS` |
| `GET /api/tickets` | `ServiceTickets` (todos los estados, incluidos cerrados) | volumen de soporte × tiempo | ✅ `EXISTS` |
| `GET /api/meter-readings` | `Assets` instalados + todas sus lecturas | flota × tiempo | — (staff) |
| `GET /api/assets/{id}/meter-readings` | `MeterReadings` de un activo | tiempo | — |
| `GET /api/contracts` | `Contracts` | clientes | ✅ directa |
| `GET /api/maintenance-orders` | `MaintenanceOrders` | flota × tiempo | — |
| `GET /api/maintenance-schedules` | `MaintenanceSchedules` + lecturas + re-consulta | flota | — |
| `GET /api/maintenance-schedules/{id}/orders` | `MaintenanceOrders` | tiempo | — |
| `GET /api/clients` | `Clients` | clientes | — |
| `GET /api/locations` | `ClientLocations` (todas, de todos) | clientes × sedes | ✅ directa |
| `GET /api/clients/{id}/locations` | `ClientLocations` | sedes | ✅ directa |
| `GET /api/users` | `Users` | plantilla | — |
| `GET /api/technicians` | `Technicians` | plantilla | — |
| `GET /api/technicians/{id}/timelogs` | `TimeLogs` | **visitas × tiempo** | — |
| `GET /api/cities` | `Cities` (~1.100 filas fijas) | no crece | — |
| `GET /api/assets/{id}/status-history` | `AssetStatusLogs` | tiempo | — |
| `GET /api/tickets|orders/{id}/assignment-history` | `AssignmentHistories` | tiempo | — |
| `GET /api/technicians/me/coverage-*` (3) | tickets/órdenes/cronogramas en cobertura | flota × tiempo | — |

### 4.2 Riesgo combinado con RLS — priorización

**El mecanismo, con precisión.** Cuando llega una consulta con un token de Cliente, Postgres inyecta el predicado `USING` de la política como filtro **antes** de devolver nada a la aplicación. Sin `LIMIT`, ese predicado se evalúa sobre **todas las filas candidatas de la tabla**, no sobre las que el usuario terminará viendo. Un cliente con 12 activos, en una base con 10.000, dispara 10.000 evaluaciones del `EXISTS` (10.000 sondas a la PK de `ClientLocations`) para devolver 12 filas. Con `LIMIT 25` + un índice sargable, el planner puede parar temprano.

Y por §3.2, en `ClientLocations`/`Contracts` el cast `::text` hace que ni siquiera el predicado directo use índice. La combinación **"sin paginación" + "predicado no sargable"** es lo que convierte esto en un problema compuesto, no en dos problemas independientes.

**Ranking de urgencia:**

**#1 — `GET /api/assets` bajo rol Cliente. El más urgente, con diferencia.**
Es el único endpoint que combina las tres cosas a la vez: (a) `Assets` es de las tablas más grandes y la política es un `EXISTS` evaluado fila por fila sobre toda la tabla; (b) sin paginación, no hay corte temprano posible; (c) después del filtrado, `AttachLastMeterReadingsAsync` (§2.4) trae **todas las lecturas** de todos los activos devueltos. Es el producto de un scan completo con una carga hija ilimitada. Y es una pantalla del portal del cliente (`MyAssetsView.vue`), es decir, tráfico externo.

**#2 — `GET /api/tickets` bajo rol Cliente.**
`ServiceTickets` es la tabla de crecimiento más rápido del sistema (una fila por incidencia, y nunca se purga), tiene política `EXISTS`, y el listado **no filtra por estado**: devuelve `Cerrado` y `Cancelado` junto con los abiertos. A dos años de operación, un cliente que abre 5 tickets al mes recibe 120 filas de las que le interesan 3, y el motor evaluó el predicado sobre el histórico completo de *todos* los clientes.

**#3 — `GET /api/meter-readings` (staff/técnico).**
No pasa por RLS (staff hace bypass), pero es el payload bruto más grande: todos los activos instalados **más todas las lecturas de todos ellos** materializadas en memoria del servidor. Es el endpoint que primero va a provocar un pico de memoria o un timeout, independientemente de RLS.

**#4 — `GET /api/technicians/{id}/timelogs`.**
Crece monótonamente con cada visita y jamás se poda. Sin RLS y de uso interno, pero es el caso más claro de serie temporal sin techo.

**#5 — `GET /api/contracts` bajo rol Cliente.**
Política directa con cast no sargable → seq scan de `Contracts`. Tabla pequeña (un puñado de contratos por cliente), así que la urgencia es baja; se resuelve gratis con el arreglo de §3.2.

**Bajo:** `clients`, `users`, `technicians`, `locations` — tablas acotadas por el tamaño del negocio, no por el tiempo. Necesitan paginación por higiene y por el tope de seguridad, no por riesgo real.

### 4.3 Patrón recomendado por caso de uso

**Cursor (keyset) — para las series temporales append-only.** Estas ya se ordenan `DESC` por una columna monótona, y su problema es que crecen sin techo:

`tickets`, `maintenance-orders`, `assets/{id}/meter-readings`, `technicians/{id}/timelogs`, `assets/{id}/status-history`, `*/assignment-history`, `maintenance-schedules/{id}/orders`.

Cursor = tupla `(CreatedAt, Id)` codificada en base64 (el `Id` desempata timestamps idénticos, que aquí son posibles porque varias filas se crean en el mismo `SaveChanges`):

```csharp
query.Where(x => x.CreatedAt < cursor.CreatedAt
              || (x.CreatedAt == cursor.CreatedAt && x.Id < cursor.Id))
     .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
     .Take(pageSize + 1)   // +1 para saber si hay página siguiente
```

Por qué cursor y no offset aquí: (a) el costo es constante en cualquier profundidad de página, mientras que `OFFSET 5000` hace que Postgres genere y descarte 5.000 filas; (b) es estable ante inserciones concurrentes — con offset, un ticket nuevo mientras el usuario pagina desplaza todo y le muestra filas repetidas; (c) sobre estas tablas la inserción concurrente es lo normal, no la excepción. Requiere los índices de §3.3 (`(CreatedAt DESC)`), que hay que crear de todos modos.

**Offset — para los catálogos administrativos acotados.**

`clients`, `users`, `technicians`, `locations`, `contracts`, `asset-brands`, `asset-models`.

Aquí el usuario quiere saltar a una página concreta, quiere ver el total ("143 clientes"), y el conjunto no crece con el tiempo sino con el negocio (miles como mucho). `COUNT(*)` + `OFFSET/LIMIT` es más simple de implementar y de consumir desde `el-pagination`, y sus desventajas no aplican a este volumen.

**Caso mixto — `assets` y `meter-readings`: primero hay que mover los filtros al servidor.**

Estos dos no se pueden paginar tal como están, porque hoy el frontend **descarga todo y filtra en el cliente**: [AssetsListView.vue:126-157](frontend-web/src/views/assets/AssetsListView.vue#L126) filtra por ciudad, cliente y estado en `computed`, y además construye las opciones de los tres desplegables a partir del conjunto completo. Paginar el backend sin mover esos filtros rompería la UX: los desplegables mostrarían solo los valores presentes en la página actual.

Secuencia correcta: (1) mover `cityId`, `clientId`, `lifecycleStatus` a query string; (2) exponer las opciones de filtro como un endpoint aparte o como facetas en la respuesta; (3) recién entonces paginar (offset sirve, con los filtros aplicados el conjunto se achica mucho).

**Las vistas agrupadas son incompatibles con paginación tal como están escritas.**
`AssetsListView`, `MaintenanceSchedulesView` y `MeterReadingsView` agrupan en el cliente en tres niveles (ciudad → cliente → contrato) sobre el resultado completo. Una página de 50 filas produciría grupos truncados sin sentido. Dos salidas: (a) endpoint de agregación que devuelva `(ciudad, cliente, contrato, count)` para pintar el árbol, y carga perezosa de las filas al expandir un grupo; (b) convertir la vista agrupada en tabla plana paginada con el grupo como filtro. La (a) conserva la UX actual y es la que recomiendo, pero es más trabajo.

**Contrato compartido.** Definir en `Toner.Application/Common` un `PagedRequest` (con `MaxPageSize = 200` y `DefaultPageSize = 50` como constantes, validados) y un `PagedResult<T> { Items, NextCursor | TotalCount, HasMore }`. Aplicar el tope **incluso a los endpoints que hoy parezcan chicos** — el valor principal del tope no es rendimiento, es que ningún endpoint futuro pueda volverse ilimitado por descuido.

---

## 5. Cache

### 5.1 Estado actual: no hay ninguna capa de cache

> **Actualizado (`5a8bc5d`).** Esta sección describe el punto de partida. Los candidatos 1, 2 y 3 de §5.3 ya están implementados con `IMemoryCache`; el resto del análisis sigue vigente, incluida la regla de §5.2 —que resultó aplicar al dashboard, y no solo a los listados por cliente.

Verificado: cero referencias a `IMemoryCache`, `IDistributedCache`, `AddOutputCache`, `[ResponseCache]` o encabezados HTTP de cache en todo el backend. En el frontend, ninguna vista cachea nada: cada navegación re-dispara todos sus `loadX()` desde cero.

Consecuencia para la pregunta sobre invalidación mal manejada: **no hay invalidación mal manejada porque no hay cache**. El hallazgo es la ausencia total, no una implementación defectuosa.

### 5.2 Regla de seguridad previa (crítica)

Antes de cachear nada: **toda clave de cache sobre datos filtrados por RLS debe incluir el tenant.** Un `IMemoryCache` con clave `"assets:list"` compartida entre un Cliente A y un Cliente B es una fuga de datos entre clientes que **evade simultáneamente el filtrado en C# y las políticas RLS** — precisamente las dos capas que el proyecto construyó para esto. Sería el peor bug de seguridad posible en esta arquitectura, y es fácil de introducir sin querer.

Por eso todos los candidatos que recomiendo abajo son **independientes del tenant o exclusivos de staff**. Cachear datos por cliente es posible (clave `$"assets:{clientId}:{page}"`) pero exige disciplina y no lo recomiendo hasta que haya una necesidad medida.

### 5.3 Candidatos, en orden de retorno sobre esfuerzo

**1. `GET /api/cities` — el mejor candidato del proyecto, sin discusión.**
~1.100 ciudades colombianas sembradas por migración desde `colombia-cities.json` (32 departamentos). **No existe ningún endpoint de escritura sobre `Cities`**: el dato es inmutable en tiempo de ejecución. Se consulta desde `CreateClientDialog`, `UsersView` y `ClientDetailView`, cada vez que se abre el diálogo.
→ **Implementado (`5a8bc5d`)** con `IMemoryCache` y TTL absoluto de 4 h. **Invalidación: ninguna** —no hay endpoint de escritura que invalidar—, solo el TTL o un deploy. Elimina ~1.100 filas de tráfico por cada apertura de formulario.

**2. `GET /api/dashboard/summary` — el mayor ahorro de trabajo del servidor.**
Es la consulta más cara del sistema (4 scans completos sin índices + agregación en memoria, §2.8/§3.7). Se dispara en `onMounted` **y en cada clic del selector de período** ([DashboardView.vue:91](frontend-web/src/views/DashboardView.vue#L91): `watch(periodDays, loadSummary)`). Son métricas operativas donde 60 segundos de desactualización no le importan a nadie.
→ ~~`OutputCache` con clave por `periodDays`, 60–300 s. Es solo-staff, así que no hay riesgo de tenant.~~ **Corregido al implementar (`5a8bc5d`): la premisa de "solo-staff" era falsa.** `DashboardService` consulta `Assets`, `ServiceTickets` y `Contracts` —las tres con política RLS— y responde también a usuarios Cliente, con datos ya filtrados por su tenant. Una clave compartida por `periodDays` habría sido exactamente la fuga de §5.2. Implementado con `IMemoryCache` y clave `dashboard:summary:{staff|client:<id>}:{periodDays}`, TTL 30 s, con `periodDays` **normalizado antes** de construir la clave (si no, `0`, `-5` y `30` generan tres entradas idénticas). **Invalidación: por expiración, no hace falta activa.**

**3. Catálogo de marcas y modelos (`AssetBrands`, `AssetModels`).**
Cambian rara vez (alta manual desde el diálogo de activos), se leen constantemente: los listados de marcas/modelos alimentan varios formularios, y `AssetModel` participa como join en las proyecciones de activos, órdenes y cronogramas.
→ **Implementado (`5a8bc5d`)** con `IMemoryCache`, TTL 4 h y una entrada por `brandId` en los modelos. **Invalidación activa** en los 3 caminos de escritura previstos (`AssetBrandService.CreateAsync`, `AssetModelService.CreateAsync`/`UpdateAsync`); la de modelos borra solo la marca afectada, no todo el catálogo.

**4. Tabla `Roles`.**
[UserService.CreateAsync:38](backend/src/Toner.Application/Users/UserService.cs#L38) consulta `Roles` por nombre en cada alta de usuario. Son 5 filas inmutables sembradas por `DataSeeder`.
→ Diccionario `nombre → Id` cacheado indefinidamente. Ahorro pequeño pero el esfuerzo es trivial.

**5. Umbrales de `AssetModel` dentro del job diario.**
El job carga `Asset` → `AssetModel` una vez por cronograma para leer 5 enteros (§3.6). Si se rediseña el job según §2.2, esto se resuelve solo. Si no, un diccionario cargado al inicio del job elimina un join por activo.

### 5.4 Dónde **no** cachear

- Cualquier listado de activos, tickets, contratos o sedes **por cliente** — ver §5.2. El beneficio no justifica el riesgo hasta tener una necesidad medida y un esquema de claves revisado.
- El `SecurityStamp` — ya evaluado y descartado con buen criterio ([Program.cs:205](backend/src/Toner.Api/Program.cs#L205)); la decisión sigue siendo correcta.
- Estados de tickets, órdenes y activos — son el dato en tiempo real que la operación necesita fresco.

### 5.5 Un pseudo-cache que sí está mal gestionado (frontend)

[stores/auth.ts](frontend-web/src/stores/auth.ts) persiste el objeto `user` completo en `localStorage` y **nunca lo revalida** contra `GET /api/auth/me`. Si un administrador le cambia el rol a un usuario, o le asigna un cliente distinto, la SPA sigue mostrando el menú y las rutas del rol viejo hasta que el usuario cierre sesión.

No es un agujero de seguridad — el backend rechaza las llamadas y el `SecurityStampValidator` invalida el token en los cambios que importan — pero es un bug de UX confuso (el usuario ve opciones de menú que dan 403). Revalidar en `onMounted` de `AppLayout`, o simplemente confiar en el `/me` en vez de en `localStorage` para todo salvo el arranque optimista, lo resuelve.

### 5.6 Cache de catálogos en el cliente (complementario)

Independiente del servidor: hoy la lista de ciudades se descarga en 3 vistas distintas, la de clientes en 4, y la de marcas en 3. Un store de Pinia `catalogs` con semántica de "buscar una vez por sesión" elimina esas peticiones repetidas sin tocar el backend, y encaja naturalmente con el cache de servidor de §5.3. Ver también §6.5.

---

## 6. Rendimiento — Frontend (Vue 3)

### 6.1 Bundle size — el mayor problema del frontend, y el más fácil de arreglar

[main.ts:20-25](frontend-web/src/main.ts#L20):

```ts
app.use(ElementPlus, { locale: es })                 // ← la librería completa, ~80 componentes

for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component)                      // ← ~1.200 componentes de icono, TODOS
}
```

Más `import 'element-plus/dist/index.css'` y `'element-plus/theme-chalk/dark/css-vars.css'` completos.

Lo que hace esto especialmente sangrante: **las vistas ya importan sus iconos individualmente**. `import { Plus, Printer } from '@element-plus/icons-vue'` en `AssetsListView`, `{ Calendar, Printer, Tickets, Tools }` en `MyWorkView`, etc. El registro global del bucle es **puro desperdicio**: 1.200 componentes SVG en el bundle de entrada de los que se usan ~15, y ninguno de ellos hacía falta registrar porque ya se importan donde se usan.

Acciones, en orden de impacto:

1. **Borrar el bucle de iconos** (líneas 23–25). Los iconos usados ya vienen por import explícito. Es un `git rm` de 3 líneas con un impacto de cientos de KB.
2. **Importación bajo demanda de componentes**: `unplugin-vue-components` + `unplugin-auto-import` con `ElementPlusResolver` en `vite.config.ts`. Reemplaza `app.use(ElementPlus)` por resolución automática por componente, y trae solo el CSS de los componentes usados. Es la configuración estándar documentada por Element Plus.
3. **`manualChunks`** en Rollup para separar `element-plus` en su propio chunk cacheable a largo plazo, distinto del código de aplicación que cambia en cada deploy.

Estimación conservadora: de ~1 MB gzip a ~250–350 KB. Es el único cambio del informe que el usuario final va a *notar* directamente.

**Code splitting por ruta: ya está bien hecho.** Las 22 rutas de [router/index.ts](frontend-web/src/router/index.ts) usan `() => import(...)`. El problema es que la ganancia queda anulada porque Element Plus completo vive en el chunk de entrada compartido — arreglar el punto 2 es lo que hace que el splitting existente empiece a rendir.

### 6.2 Reactividad

**`watch` vs `computed`: el uso es correcto en general.** Los 9 `watch` del proyecto son todos efectos secundarios legítimos (escribir en `localStorage`, disparar una petición, resetear un campo dependiente), no derivaciones disfrazadas. `watch(periodDays, loadSummary)` es exactamente para lo que sirve `watch`.

**Cero `deep: true` en todo el proyecto** ✅ — no hay watchers profundos sobre objetos grandes.

Un watcher con un patrón frágil: [CreateClientDialog.vue:50](frontend-web/src/components/CreateClientDialog.vue#L50):

```ts
watch(() => locationForms.value.map((l) => l.departmentName), (newDepts, oldDepts) => { ... })
```

El getter crea un **array nuevo en cada evaluación**, así que la comparación por referencia de Vue siempre da "cambió": el callback se ejecuta ante cualquier cambio reactivo relevante del componente, y después hace su propio diff por índice para averiguar qué pasó realmente. Funciona, y el comentario explica la intención (evitar N watchers individuales) — pero un `@change` en el `el-select` de departamento de cada fila hace lo mismo en O(1), sin watcher, y es más directo de leer.

**`reactive` vs `ref`: aplicación consistente y correcta.** Formularios con `reactive({...})`, listas y valores escalares con `ref()`. No hay confusión entre ambos ni pérdida de reactividad por desestructuración.

Un caso donde `reactive` complica: `checkoutForm` en [MyWorkView.vue:26](frontend-web/src/views/technicians/MyWorkView.vue#L26) tiene 11 campos y, al ser `reactive`, hay que resetearlo **campo por campo** — 11 líneas de asignación en [doCheckOut:223-233](frontend-web/src/views/technicians/MyWorkView.vue#L223), duplicando el bloque de inicialización. Con `ref(createEmptyCheckoutForm())`, el reset es `form.value = createEmptyCheckoutForm()`. Lo mismo aplica a `quickModelForm` en [AssetsListView.vue:89](frontend-web/src/views/assets/AssetsListView.vue#L89), donde los 6 valores por defecto (`30000`, `6`, `30000`, `6`, `60000`) están escritos **dos veces**: en la inicialización y en `openQuickModelDialog()`.

**`shallowRef` / `shallowReactive`: ausentes, y hay un caso de libro para usarlos.**

Ni uno solo en todo el proyecto. Pero las refs de listas —`assets`, `tickets`, `coverageTickets`, `orders`, `coverageOrders`, `pendingInstallations`, `coverageSchedules`, `contracts`, `clients`, `cities`— contienen DTOs planos del servidor que **siempre se reemplazan enteros** (`assets.value = res.data`) y **nunca se mutan campo a campo**. Vue está creando un proxy reactivo profundo para cada fila y cada propiedad de cada fila, para nada.

`shallowRef` sobre esas refs mantiene la reactividad al reemplazo (que es lo único que ocurre) y elimina toda la conversión profunda. Con 500 filas × 15 campos son 7.500 proxies que dejan de crearse en cada carga. Es el caso canónico de `shallowRef` y no se está aprovechando. Cambio de bajo riesgo: si alguna vista mutase una fila in-place dejaría de reaccionar, y por lo que revisé ninguna lo hace (todas recargan con `loadX()` tras escribir).

### 6.3 Re-renders y trabajo dentro de `<template>`

**a) `.reduce()` anidados dentro del template, para mostrar un contador de grupo:**

[AssetsListView.vue:380](frontend-web/src/views/assets/AssetsListView.vue#L380):
```vue
{{ cityGroup.city }} ({{ cityGroup.clientGroups.reduce((n, g) => n + g.contractGroups.reduce((m, c) => m + c.assets.length, 0), 0) }})
```
Y de nuevo en [:389](frontend-web/src/views/assets/AssetsListView.vue#L389). Idéntico en [MaintenanceSchedulesView.vue:346](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L346) y [:355](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L355).

Se reejecuta en cada render de la cabecera del colapsable, con complejidad O(clientes × contratos) por cabecera. El total ya se calcula implícitamente al construir `groupedByCity`: basta con guardarlo como campo `assetCount` en cada objeto de grupo y el template queda en `{{ cityGroup.assetCount }}`.

**b) `Intl` dentro de celdas de tabla — el costo oculto más grande:**

```vue
{{ new Date(row.startDate).toLocaleDateString(undefined, { timeZone: 'UTC' }) }}
```

En [ContractsListView.vue:148-149](frontend-web/src/views/contracts/ContractsListView.vue#L148), [MyContractsView.vue:44-46](frontend-web/src/views/portal/MyContractsView.vue#L44), [AssetsListView.vue:167-168](frontend-web/src/views/assets/AssetsListView.vue#L167), y **cuatro veces por fila** en `MaintenanceSchedulesView` (tanto en la vista plana como en la agrupada, líneas [300](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L300), [316](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L316), [383](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L383), [399](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L399)).

`toLocaleDateString` construye un `Intl.DateTimeFormat` nuevo en cada llamada, y es de las operaciones más caras del runtime de JS. Con 500 cronogramas × 4 celdas son **2.000 instanciaciones de `Intl` por render**. Un formateador a nivel de módulo (`const fechaCorta = new Intl.DateTimeFormat('es-CO', { timeZone: 'UTC' })`) reutilizado en una función `formatDate(v)` reduce eso a 1 instanciación por sesión. Cambio pequeño, ganancia grande y medible.

**c) Múltiples pasadas sobre el mismo array:**

[AssetsListView.vue:141-157](frontend-web/src/views/assets/AssetsListView.vue#L141): `cityOptions`, `clientOptions`, `statusOptions` y `filteredAssets` recorren cada uno el array completo llamando a `matches()`, y `filteredAssets` lo llama **3 veces por activo**. Son ~6 pasadas por cambio de filtro. Con 200 activos es irrelevante; con 5.000 (que es donde estamos yendo sin paginación) empieza a notarse en el hilo principal. Una sola pasada que construya las tres listas de opciones y el filtrado a la vez resuelve lo mismo.

**d) Función creada en línea como prop:**

[AssetsListView.vue:357](frontend-web/src/views/assets/AssetsListView.vue#L357): `:sort-method="(a: AssetDto, b: AssetDto) => ..."` crea una función nueva en cada render, lo que hace que la columna vea un prop "cambiado" y se re-diffee. Sacarla a una constante del `<script setup>` lo evita. Menor, pero es el ejemplo del patrón "objeto/función nueva como prop en cada render" que se preguntó.

### 6.4 Listas grandes: sin virtualización y sin paginación

Ninguna tabla del proyecto está virtualizada. Element Plus incluye `el-table-v2` (virtual scroll) precisamente para esto, y no se usa en ninguna vista. Combinado con §4 (cero paginación), las tablas de activos, tickets, lecturas y cronogramas renderizan **una fila del DOM por cada fila de la base de datos**. Con 2.000 activos son ~2.000 `<tr>` con 8 celdas cada uno, muchas con `el-tag` y `el-tooltip` anidados dentro.

El orden correcto de ataque es paginación primero (§4), virtualización después y solo si sigue haciendo falta — paginar resuelve el problema de red *y* el de DOM a la vez.

**`:key` en `v-for`: correcto en el 100 % de los casos.** Verificado con un escaneo por etiqueta (no por línea): las 15 directivas `v-for` del proyecto tienen `:key` con un identificador estable (`id`, `clientKey`, una clave compuesta `ciudad::cliente::contrato`, o el valor mismo cuando es un string único). Ni un solo `:key="index"`. ✅

### 6.5 Estado global (Pinia)

**Un solo store, `auth`, organizado por feature y correctamente acotado.** Contiene exactamente lo que debe ser global: token, usuario y los helpers de rol. No hay nada ahí que debiera ser local. El problema opuesto sí existe.

**Falta estado compartido que hoy se re-descarga.** No hay store de catálogos, así que:
- `cities` se descarga por separado en `CreateClientDialog`, `UsersView` y `ClientDetailView`.
- `clients` se descarga en `TicketsListView`, `ContractsListView`, `AssetsListView` y `ClientsListView`.
- `assetBrands` se descarga en `AssetsListView`, `AssetBrandsView` y `AssetBrandDetailView`.

Cada navegación entre esas vistas repite la descarga completa. Un store `catalogs` con semántica de *fetch-once* (`if (cities.value.length) return`) elimina el tráfico redundante y encaja con el cache de servidor de §5.3.

**Un caso extremo de peticiones en paralelo:** [MyWorkView.loadAll](frontend-web/src/views/technicians/MyWorkView.vue#L109) dispara **7 peticiones HTTP simultáneas**, y se vuelve a llamar entero después de *cada* check-in, check-out o claim (6 sitios). Cada una de esas 7 peticiones paga su propio `SELECT` de `SecurityStamp` + sus `set_config` (§2.1). Un endpoint agregado `GET /api/technicians/me/dashboard` que devuelva las 7 colecciones en una sola respuesta reduciría 7 requests × ~4 round-trips a 1 request. Es el mejor argumento concreto a favor de un endpoint compuesto en todo el proyecto.

---

## 7. Código limpio

### 7.1 Unidades demasiado largas

| Archivo / método | Líneas | Responsabilidades |
|---|---|---|
| [MyWorkView.vue](frontend-web/src/views/technicians/MyWorkView.vue) | 610 | Estado del técnico + 3 tipos de trabajo + 2 pestañas + formulario de check-out de 11 campos |
| [AssetsListView.vue](frontend-web/src/views/assets/AssetsListView.vue) | 559 | Listado + agrupación 3 niveles + 3 filtros + alta de activo + alta rápida de marca + alta rápida de modelo |
| [AssetService.cs](backend/src/Toner.Application/Assets/AssetService.cs) | 529 | CRUD + máquina de estados + lecturas + 2 listados especializados (§1.2d) |
| [MaintenanceSchedulesView.vue](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue) | 494 | Listado plano + agrupado + tooltips de 3 sub-reglas |
| [Program.cs](backend/src/Toner.Api/Program.cs) | 344 | Composition root sin estructura (§1.2e) |
| [TechnicianCheckInService.CheckOutAsync](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L148) | ~180 | 3 flujos de check-out en un método (§1.2c) |

### 7.2 Código duplicado

- **`CurrentUser` (claims) — 5 copias literales** en controllers (§1.2f).
- **"Última lectura por activo" — 4 copias** de la misma agrupación en memoria (§2.4).
- **Agrupación tri-nivel `groupedByCity` — 3 copias** de ~40 líneas en el frontend (§1.3).
- **`departmentsFor()` / `citiesInDepartment()` — 3 copias**, documentado por el propio código.
- **`statusTagType()` — 6+ copias** con ramas divergentes (`MyAssetsView` omite `PendienteInstalacion`, que `AssetsListView` sí trata; ya divergieron).
- **Valores por defecto de umbrales de modelo — 2 copias** en el mismo archivo ([AssetsListView.vue:91-96](frontend-web/src/views/assets/AssetsListView.vue#L91) y [:100-104](frontend-web/src/views/assets/AssetsListView.vue#L100)).
- **`err.response?.data?.title ?? '…'` — ~30 copias** (§8.3).

Duplicación *aceptable* y que no recomiendo tocar: el trío `Projected` / `ProjectedFrom` / `ToDtoAsync` repetido en 8 servicios. Es un idioma consistente, cada instancia proyecta un DTO distinto, y abstraerlo produciría genéricos peores de leer que la repetición.

### 7.3 Nullable reference types de C#

`<Nullable>enable</Nullable>` está activo en **los 4 proyectos** ✅ y el uso es disciplinado: `Guid?`/`string?` donde corresponde, el operador `!` aparece solo después de una guarda que ya probó no-nulidad (p. ej. [AuthService:70](backend/src/Toner.Application/Auth/AuthService.cs#L70), donde `lockoutJustExpired` implica `user is not null`).

Tres puntos ásperos:

**a) `Guid.Parse(User.FindFirstValue(...)!)` en las 5 copias de `CurrentUser`.** Si el token carece del claim, esto lanza `ArgumentNullException`; si trae basura, `FormatException`. Ambas caen en el `_ =>` del middleware → **HTTP 500**, cuando lo correcto sería 401. El `!` está silenciando al compilador sobre un valor que viene de fuera del sistema. `Guid.TryParse` + un 401 explícito es lo correcto, y al centralizar la propiedad (§1.2f) se arregla en un solo sitio.

**b) Indexadores de diccionario sin guarda.** `AllowedTransitions[asset.LifecycleStatus]` ([AssetService:195](backend/src/Toner.Application/Assets/AssetService.cs#L195)), `AllowedTransitions[ticket.Status]` ([ServiceTicketService:211](backend/src/Toner.Application/Tickets/ServiceTicketService.cs#L211)) y `SlaTargetHours[priority]` ([DashboardService:154](backend/src/Toner.Application/Dashboard/DashboardService.cs#L154)) lanzarían `KeyNotFoundException` → 500 si alguien agrega un valor al enum sin actualizar el diccionario. Hoy los diccionarios son exhaustivos, pero nada lo obliga. Un test que recorra `Enum.GetValues<T>()` y verifique que cada valor tiene entrada es la salvaguarda más barata; una `switch expression` con brazo `_ =>` es la alternativa estructural.

**c) `Enum.Parse<T>` sobre entrada del usuario** — ver §8.2.

### 7.4 Números y cadenas mágicas

**Bien resuelto (por encima del promedio):** `LeadDays`/`LeadPrints`, `SlaTargetHours`, `MaintenanceOnTimeWindowDays`, `AssumedHoursPerDay`, `MaxFailedLoginAttempts`, `LockoutDuration`, `DecoyPasswordHash`, `RoleNames.*`, `PasswordDefaults`. Todas las constantes de negocio del backend tienen nombre y, en la mayoría de los casos, un comentario que explica de dónde salió el valor.

**Pendiente:**

| Literal | Dónde | Debería ser |
|---|---|---|
| `"MG"`, `"MU"`, `"CI"` | [MaintenanceScheduleService.ComboCodes:185](backend/src/Toner.Application/Maintenance/MaintenanceScheduleService.cs#L185) y consumido crudo por el frontend como texto de `el-tag` | Enum o constantes compartidas por el contrato del DTO — hoy es un acoplamiento por string entre capas sin un solo punto de verdad |
| `"Check-in"` / `"Check-out"` | [TechnicianCheckInService:131](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L131), [:228](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L228) como `TechnicianAvailability.Reason` | Constantes; es un valor que se persiste y sobre el que se podría querer filtrar |
| `30.44` (días por mes) | [ContractAssetService:145](backend/src/Toner.Application/Contracts/ContractAssetService.cs#L145) | Constante con nombre — el comentario explica la fórmula pero el número está suelto |
| `5` / `100` / `TimeSpan.FromMinutes(1)` | [Program.cs:260,271](backend/src/Toner.Api/Program.cs#L260) | Son política de seguridad, no configuración de framework — a `appsettings` o a constantes con nombre |
| `30000` / `6` / `60000` | [AssetsListView.vue:91,100](frontend-web/src/views/assets/AssetsListView.vue#L91) ×2 | Constante del módulo; hoy están escritos dos veces en el mismo archivo |
| `'Instalado'`, `'Asignado'`, `'EnProceso'`, `'EnBodega'`, `'Ocupado'`, `'PendienteInstalacion'` | comparados como literales crudos en ~8 vistas | `api/types.ts` **ya define** los mapas de etiquetas de estos enums; falta exportar también las constantes de valor y usarlas en las comparaciones |
| `'toner_token'`, `'toner_user'`, `'sidebar-collapsed'` | claves de `localStorage` en 2 archivos | Ya son constantes en `auth.ts` ✅; `'sidebar-collapsed'` en `AppLayout` no |

### 7.5 Nombres

Buenos en general, con vocabulario de dominio consistente en español y sin abreviaturas crípticas. Tres observaciones:

- **`request.InitialCounterValue` miente en 2 de sus 3 usos.** En [CheckOutRequest](backend/src/Toner.Application/Technicians/Dtos/CheckOutRequest.cs) el campo se llama "contador inicial" (correcto para una instalación) pero se reutiliza como el contador **final** al completar una orden de mantenimiento ([TechnicianCheckInService:320](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L320)) y como el contador de la visita al cerrar un ticket ([:280](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L280)). `CounterValue` a secas sería honesto en los tres casos.
- **`Projected` / `ProjectedFrom`** no dicen qué proyectan. `AsDto` / `ToDtoQuery` sería más directo, aunque el patrón es tan repetido que se aprende rápido.
- **`AttachLastMeterReadingsAsync` / `AttachLastKnownCountersAsync` / `AttachAssetMetricsAsync`**: tres nombres distintos para (esencialmente) la misma operación en tres servicios. Al unificarlas (§2.4) conviene un nombre único.

### 7.6 Lógica de negocio en componentes en vez de composables

Ya cubierto en §1.3. Los casos concretos con más lógica atrapada en la vista:

- La agrupación tri-nivel ciudad→cliente→contrato (3 vistas, ~40 líneas cada una) — es una transformación pura, ideal para `useCityGrouping(items, keyFns)`.
- `checkoutFormValid` en [MyWorkView.vue:89](frontend-web/src/views/technicians/MyWorkView.vue#L89) — reglas de validación de negocio (qué campos exige cada tipo de check-out) que **duplican** las validaciones del servidor en [CheckOutAsync:182-204](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L182). La duplicación en sí es correcta (UX), pero debería vivir en un composable junto al resto de la lógica de check-out, no dentro de un componente de 610 líneas.
- El diff de departamento/ciudad en `CreateClientDialog`, `UsersView`, `ClientDetailView` → `useColombianCities()`.

### 7.7 Comentarios — una fortaleza que conviene señalar

Es lo mejor del código. Los comentarios explican consistentemente el *por qué* y no el *qué*, documentan alternativas descartadas con su razón (por qué `set_config` y no `SET`, por qué sesión y no `SET LOCAL`, por qué no se cachea el `SecurityStamp`), advierten de trampas futuras (`FORCE ROW LEVEL SECURITY` en migraciones), y remiten a los hallazgos numerados de las auditorías. Esto es raro y vale mucho para el mantenimiento.

Un riesgo asociado: varios comentarios afirman invariantes que dependen de configuración externa. El de `NpgsqlSessionResetTests` **sí** está respaldado por un test, y el de "a lo sumo un `ContractAsset` activo" **sí** está respaldado por un índice único parcial — bien. Pero el de [ContractAssetConfiguration](backend/src/Toner.Infrastructure/Persistence/Configurations/ContractAssetConfiguration.cs) sobre la traducción de la violación de unicidad depende de que `TonerDbContext.TryGetUniqueViolationMessage` siga existiendo; y el comentario de `EnableRetryOnFailure` sobre "sin transacciones explícitas (verificado)" dejará de ser cierto en cuanto se implemente §8.4, y ahí hay que acordarse de envolver las transacciones en el execution strategy.


### 7.8 Drift del sistema de diseño (hallazgo #41)

Detectado en [MaintenanceSchedulesView.vue](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue). **Es deuda preexistente**, del commit `883bfa2`, no introducida por ninguna de las tandas de correcciones de este audit. Severidad **Baja / Cosmético**; **sin tanda asignada todavía** — el segundo punto depende de una decisión de diseño que no es técnica.

**1. `font-size` literal en vez de token — sistémico, no local.**
[L466](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L466) y [L497](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L497) declaran `font-size: 0.85rem` crudo. El valor coincide exactamente con el paso `--text-md` de la escala tipográfica de `DESIGN.md`, que enuncia la regla explícitamente: *"Font sizes are always `var(--text-*)`; a literal `font-size` value in board-world CSS is drift, not a new step"*.

Lo relevante es el alcance: `font-size: 0.85rem` aparece en **13 archivos** del frontend. Cambiarlo solo en esta vista sería un parche arbitrario que deja la regla igual de rota en las otras 12. **Pide un barrido sistémico** —idealmente con una regla de lint que lo impida a futuro—, no una corrección puntual.

**2. Cuatro niveles de urgencia contra una paleta de dos — decisión de diseño pendiente.**

El drift **no está solo en el bloque `<style>`; también en el `<script>`**, y el alcance es mayor de lo que sugieren las filas de la tabla. `urgencyColors` ([L156-161](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L156)) define **cuatro** niveles, ninguno con un color de la paleta:

| Nivel | Literal | Qué es realmente | Fila teñida |
|---|---|---|---|
| `far` | `#67c23a` | verde `success` de Element Plus | no |
| `soon` | `#eab308` / `rgba(234, 179, 8, 0.08)` ([L501](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L501)) | ámbar de Tailwind, no `signal-amber` (`#d9a441`) | sí |
| `urgent` | `#f97316` / `rgba(249, 115, 22, 0.1)` ([L505](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L505)) | naranja **que no existe en la paleta**, en ningún tono | sí |
| `overdue` | `#f56c6c` / `rgba(245, 108, 108, 0.12)` ([L509](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L509)) | `danger` de Element Plus, no `signal-red` (`#d64545`) | sí |

A eso se suma el `color: '#fff'` de `urgencyTagStyle` ([L171](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L171)): la tinta del sistema es `flap-ink` (`#eef0ec`, blanco cálido), y el blanco puro solo está sancionado en `DESIGN.md` para texto sobre relleno Signal Blue —el botón primario—, no como color de primer plano genérico.

La causa raíz no es descuido de valores: **`DESIGN.md` define dos lámparas de estado reservadas** —`signal-amber` para "en riesgo/retrasado" y `signal-red` para "crítico/vencido"— **pero el código implementa cuatro niveles**. El naranja de `urgent` se inventó para llenar un hueco que la paleta no cubre, y `far` se pinta de verde cuando `DESIGN.md` reserva Signal Blue para el estado "en hora / en marcha" y descarta explícitamente el verde del vocabulario de lámparas de dominio. Mientras esa brecha exista, cualquier "corrección" de los valores sería adivinar.

> **Decisión pendiente (de Oscar, no técnica):** ¿se **colapsa el nivel intermedio a ámbar** —dejando dos niveles visuales sobre los lógicos, coherente con la paleta actual—, o se **agrega `signal-orange` como token nuevo** a `DESIGN.md`, asumiendo la tercera lámpara como parte del sistema de diseño?
>
> Al resolverla hay que decidir de paso qué le corresponde a `far`: hoy verde, cuando el sistema tiene Signal Blue para ese estado.
>
> Hasta que se responda, esto no entra a ninguna tanda.

Sobre la regla *"State is never color-only"* de `DESIGN.md`: la **etiqueta sí cumple** — `urgencyLabels` ([L162-167](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L162)) acompaña cada color con texto ("Lejano", "Próximo", "Muy próximo", "Vencido"). El tinte de fila es color puro, pero no aporta información que la etiqueta de la misma fila no lleve ya, así que es redundancia visual y no un incumplimiento.

---

## 8. Manejo de errores y resiliencia

### 8.1 Excepciones como control de flujo — sistemático

Hay ~60 sitios que lanzan `NotFoundException`, `ConflictException`, `ForbiddenException` o `InvalidCredentialsException` para resultados **esperados** de la operación, mapeados a códigos HTTP por [ExceptionHandlingMiddleware.Map](backend/src/Toner.Api/Middleware/ExceptionHandlingMiddleware.cs#L78).

Es un patrón habitual y defendible en ASP.NET Core, y la implementación está bien hecha (mapeo centralizado, sin fugas de detalles internos, log diferenciado para `ForbiddenException` con valor forense). Dicho eso, tiene dos costos reales:

- **Costo de ejecución.** Lanzar y desenrollar una excepción en .NET cuesta ~5–20 µs. Estas excepciones se disparan en acciones **ordinarias** del usuario: "ya existe un activo con ese número de serie", "no se puede tomar un ticket en estado Cerrado", "la lectura no puede ser menor a la última". No son casos excepcionales, son la mitad del flujo normal de un formulario.
- **La firma miente.** `Task<AssetDto> ChangeStatusAsync(...)` declara que devuelve un DTO. En realidad devuelve un DTO **o** un conflicto **o** un no-encontrado, y nada en el tipo lo dice. El llamador no tiene forma de saber qué manejar sin leer la implementación.

Un `Result<T, Error>` (propio, o `OneOf`/`FluentResults`) para las ramas *esperadas* —conflicto de validación, no encontrado, prohibido— reservando las excepciones para fallos genuinos, arregla ambas cosas. No recomiendo un refactor big-bang: aplicarlo a los servicios de más tráfico y con más ramas esperadas (ciclo de vida de activos, estados de tickets y órdenes) captura la mayor parte del valor. El middleware puede seguir existiendo para los fallos reales.

### 8.2 `Enum.Parse` sobre entrada del usuario → 500 en vez de 400

[AssetService:67](backend/src/Toner.Application/Assets/AssetService.cs#L67), [:162](backend/src/Toner.Application/Assets/AssetService.cs#L162), [:193](backend/src/Toner.Application/Assets/AssetService.cs#L193); [ServiceTicketService:59](backend/src/Toner.Application/Tickets/ServiceTicketService.cs#L59), [:209](backend/src/Toner.Application/Tickets/ServiceTicketService.cs#L209); [ContractService:85](backend/src/Toner.Application/Contracts/ContractService.cs#L85).

Un valor inválido produce `ArgumentException`, que cae en el brazo `_ =>` del middleware → **HTTP 500 "Ocurrió un error inesperado"**, cuando el error es del cliente.

Hoy los validators de FluentValidation cubren estos endpoints, así que en la práctica no se alcanza. Pero el propio código documenta que los servicios son invocables directamente ([ClientService:21](backend/src/Toner.Application/Clients/ClientService.cs#L21): *"el servicio puede llamarse directo (tests, otro servicio a futuro) sin pasar por FluentValidation"*) y aplica validación defensiva por ese motivo — el mismo criterio debería aplicarse aquí. `Enum.TryParse` + `ConflictException` es una línea por sitio.

### 8.3 `try`/`catch` vacíos o que silencian errores

**Backend: ninguno.** Verificado en todo el código. Cada `catch` o bien registra y relanza ([MaintenanceScheduleEvaluationJob:50](backend/src/Toner.Infrastructure/Jobs/MaintenanceScheduleEvaluationJob.cs#L50), que además usa `CancellationToken.None` para el log con justificación correcta), o bien mapea a una respuesta ([ExceptionHandlingMiddleware](backend/src/Toner.Api/Middleware/ExceptionHandlingMiddleware.cs), [TonerDbContext.SaveChangesAsync](backend/src/Toner.Infrastructure/Persistence/TonerDbContext.cs#L66) traduciendo `23505` a `ConflictException`). Esto está genuinamente limpio.

**Frontend: 3 catches silenciosos, uno de ellos problemático.**

- [AssetsListView.vue:82](frontend-web/src/views/assets/AssetsListView.vue#L82) — `catch { models.value = [] }`. **Correcto y documentado**: la marca ya se creó, que falle la recarga de modelos no debe reportarse como fallo de creación. Sin objeción.
- [LoginView.vue:23](frontend-web/src/views/LoginView.vue#L23) — muestra un error genérico. Aceptable en un login (no se quiere distinguir causas hacia el usuario), aunque no registrar nada en consola dificulta diagnosticar un fallo de red vs. un 500.
- [DashboardView.vue:29](frontend-web/src/views/DashboardView.vue#L29) — `catch { ElMessage.error('No se pudieron cargar los indicadores') }`. **Este sí es un problema**: descarta el error por completo. Un 403, un 500 y un timeout de red son indistinguibles para el usuario **y para quien depure**, porque no queda rastro ni en consola.

**El hueco mayor no son los catches vacíos, son los `try` sin `catch`.** En ~15 vistas el patrón de carga es:

```ts
async function loadData() {
  loading.value = true
  try {
    const { data } = await api.listX()
    items.value = data
  } finally {
    loading.value = false
  }
}
```

Sin `catch`. Si la petición falla, la promesa se rechaza sin manejar, `items` queda vacío, el spinner se apaga y **la tabla muestra su mensaje de vacío**: al usuario le dice *"No hay activos registrados"* cuando la verdad es *"la petición falló"*. Está en `MyAssetsView`, `MyContractsView`, `AssetsListView`, `MyWorkView`, `TicketsListView`, `ContractsListView`, `UsersView`, `TechniciansView`, `MeterReadingsView`, `MaintenanceSchedulesView`, `MaintenanceOrdersView` y los detalles. **Es el fallo de resiliencia más visible para el usuario final de toda la aplicación.**

### 8.4 Integridad transaccional — varios flujos con commits parciales

`EnableRetryOnFailure` está configurado, y su comentario dice: *"sin transacciones explícitas en todo el código (verificado), así que no hay conflicto con el requisito de EF Core de envolverlas en un execution strategy"*. La afirmación es **exacta como observación**, pero describe un problema, no un diseño.

Flujos con **múltiples `SaveChangesAsync` secuenciales** y por tanto múltiples transacciones implícitas independientes:

1. **[ServiceTicketService.CreateAsync:71-76](backend/src/Toner.Application/Tickets/ServiceTicketService.cs#L71)** — guarda el ticket, después `AssignmentEngine.AssignServiceTicketAsync` guarda por su cuenta ([AssignmentEngine:54](backend/src/Toner.Application/Assignment/AssignmentEngine.cs#L54)). Si falla entre medio: queda un ticket persistido en `Abierto`, sin técnico y **sin `AssignmentHistory`** — un estado que ningún proceso vuelve a mirar. El ticket queda invisible para el motor de asignación para siempre.
2. **[TechnicianCheckInService.CheckOutAsync](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L297)** — su propio `SaveChanges`, después `AssignMaintenanceOrderAsync` (otro), después `SetStatusAsync` **o** `CompleteAsync` (otro más, y `CompleteAsync` a su vez recalcula el cronograma y guarda). Hasta 4 transacciones. Un fallo intermedio deja la visita cerrada pero el ticket sin resolver, o la orden completada sin cronograma recalculado.
3. **[MaintenanceScheduleEvaluationJob](backend/src/Toner.Infrastructure/Jobs/MaintenanceScheduleEvaluationJob.cs#L86)** — un `SaveChanges` por orden dentro del loop, y las asignaciones en un segundo loop posterior. Una caída a mitad deja órdenes generadas y **sin asignar permanentemente**: la siguiente ejecución diaria no las reasigna, porque `hasOpenOrder` detecta que ya existe una orden abierta y no hace nada más.

El caso 3 es el más grave porque es silencioso y **no se autorepara**.

Corrección: una transacción explícita por operación de negocio, envuelta en el execution strategy que la política de reintentos exige (`db.Database.CreateExecutionStrategy().ExecuteAsync(async () => { using var tx = ...; ...; await tx.CommitAsync(); })`). Al hacerlo hay que actualizar el comentario de `EnableRetryOnFailure` — y, bonus, dentro de una transacción explícita `SET LOCAL` sí funciona, lo que abre la alternativa mencionada en §2.1.

### 8.5 Consistencia del manejo de errores de API en el frontend

El patrón `err.response?.data?.title ?? 'mensaje por defecto'` está repetido ~30 veces, siempre con `err: any`. Funciona porque el backend emite `application/problem+json` con `title` de forma consistente, pero tiene tres defectos:

- **Pierde los errores de campo.** Ante un 400 de FluentValidation, el backend devuelve `{ title, status, errors: { "Campo": ["mensaje"] } }` ([ExceptionHandlingMiddleware:80](backend/src/Toner.Api/Middleware/ExceptionHandlingMiddleware.cs#L80)). El frontend muestra solo `title` → *"Uno o más campos no son válidos"*, **ocultando cuál campo y por qué**. El backend hace bien su trabajo y el frontend tira la mitad de la información.
- **No distingue fallo de red de error de API.** Si `err.response` es `undefined` (timeout, DNS, CORS, servidor caído), el `??` cae al mensaje por defecto, que dice cosas como "No se pudo crear el activo" — engañoso cuando el problema es que no hay conexión.
- **`err: any` en las 30 copias** anula el tipado de un proyecto que por lo demás usa TypeScript en modo estricto.

Un único helper `handleApiError(err, fallback)` en `api/http.ts` —o mejor, un interceptor de respuesta que normalice todo a un `ApiError` tipado con `{ status, title, fieldErrors, isNetworkError }`— resuelve los tres a la vez y elimina las 30 duplicaciones.

### 8.6 Resiliencia — infraestructura

**Presente y correcto:**
- `EnableRetryOnFailure(3, 5 s)` sobre EF Core ✅
- Rate limiting global (100/min) y de auth (5/min) por IP ✅
- Bloqueo de cuenta por intentos fallidos con reinicio del contador al expirar ✅
- Traducción de violaciones de unicidad (`23505`) a conflicto de negocio — cubre las carreras verificar-luego-insertar ✅
- Registro de excepciones en BD con `DbContext` independiente, para poder loguear aunque el contexto de la request esté corrupto ✅ (diseño cuidado)
- El job de Hangfire relanza tras loguear, para que la política de reintentos se aplique ✅

**Faltante:**
- **Sin timeout en axios.** [http.ts:5](frontend-web/src/api/http.ts#L5): `axios.create({ baseURL })` sin `timeout`. Una petición colgada deja el spinner girando indefinidamente, sin salida para el usuario. Un `timeout: 30000` es una línea.
- **Sin cancelación al navegar.** Ninguna vista aborta sus peticiones en `onUnmounted`. Si el usuario navega rápido entre vistas, respuestas obsoletas pueden llegar después y sobrescribir estado (el `finally { loading = false }` de una petición vieja apaga el spinner de la nueva).
- **Sin endpoint de health check.** No hay `/health` ni `MapHealthChecks`. El contenedor no tiene forma de reportar readiness/liveness a un orquestador.
- **`MustChangePasswordMiddleware` sin cobertura de tests** (los tests de API cubren forwarded headers, rate limiting, stamp y el middleware de excepciones, pero no este).
- Sin circuit breaker / Polly — **no hace falta hoy**: no hay dependencias HTTP salientes. Los paquetes `Azure.Storage.Blobs`, `FirebaseAdmin` y `MailKit` están referenciados pero **sin usar** en el código (candidatos a quitar del `.csproj` hasta que se implementen: hoy son superficie de dependencias y de CVEs sin contrapartida).

---

## 9. Resumen priorizado

Esfuerzo: **XS** < 1 h · **S** 1–4 h · **M** 1–2 días · **L** 3–5 días · **XL** > 1 semana

### Alto impacto

| # | Hallazgo | Capa | Tipo | Impacto | Archivo | Esfuerzo |
|---|---|---|---|---|---|---|
| 1 | Element Plus completo + los ~1.200 iconos registrados globalmente; las vistas ya importan sus iconos | Frontend | Performance | **Alto** | [main.ts:20](frontend-web/src/main.ts#L20) | **XS** (borrar bucle) / **S** (resolver on-demand) |
| 2 | RLS forzaba seq scan bajo token de Cliente por el `OR` del bypass de staff dentro del predicado. Se partió cada política en dos por rol (`TO toner_app` / `TO toner_app_staff`, sin `BYPASSRLS`). Ver §3.2. | DB | Performance | **🟡 Parcialmente resuelto** — `ClientLocations` y `Contracts` confirmados con `Index Scan` (commits de esta sesión). `ServiceTickets` y `Assets` siguen en `Seq Scan`: Postgres trata el predicado RLS como *security barrier* y no sube el `EXISTS` a un join, así que no hay mejora posible sin denormalizar `ClientId` (fase 3, ya diferida). No es un bug de implementación, es una limitación estructural de RLS con `EXISTS` en Postgres, confirmada con `EXPLAIN ANALYZE`. | [SplitRlsPoliciesByRole.cs](backend/src/Toner.Infrastructure/Persistence/Migrations/20260818203015_SplitRlsPoliciesByRole.cs) | **M** (fase 3: denormalizar `ClientId`) |
| 3 | Falta `MeterReadings (AssetId, ReadingDate DESC)` — la consulta más repetida del sistema, hoy con sort | DB | Performance | **Alto** — ✅ Resuelto (`1673d8c`) | `MeterReadingConfiguration.cs` | **XS** |
| 4 | Sin paginación en ningún listado. **Pasos 1 y 2 hechos**: los 21 endpoints de los grupos A/B/C devuelven `PagedResult<T>` — cursor (keyset con desempate por `Id`) en las 6 series temporales, offset en los 15 restantes; los 5 catálogos de desplegable (Grupo D) quedan sin paginar a propósito, y `GET /api/cities` se resuelve con caché (#10), no con paginación. La capa `api/*.ts` desenvuelve `.items`, así que las 19 vistas siguen sin tocarse; `getList()` avisa por consola en dev si una página viene incompleta, como red contra el truncamiento silencioso. **Paso 3 pendiente** (UI de paginación y filtros server-side vista por vista), y con él bajar `DefaultPageSize` de 200 a `PagedUiDefaultPageSize` (50). **Pendiente menor:** el cursor de `AssetStatusLogs.ChangedAt` y `AssignmentHistories.AssignedAt` no tiene índice sobre su columna de orden — hoy irrelevante (tablas vacías, y filtran primero por el padre, que sí está indexado), pero si esas tablas crecen conviene un compuesto `(padre, fecha)`. | Backend + Frontend | Performance | **Alto** — 🟡 Pasos 1 y 2 hechos; Paso 3 pendiente | [Common/Paging/](backend/src/Toner.Application/Common/Paging/), [api/paging.ts](frontend-web/src/api/paging.ts) | **L** |
| 5 | Se materializan **todas** las `MeterReadings` para calcular un máximo — 4 copias del mismo bloque | Backend | Performance | **Alto** — ✅ Resuelto (`5d6c0ae`) | [AssetService.cs:107](backend/src/Toner.Application/Assets/AssetService.cs#L107) +3 sitios | **M** |
| 6 | N+1 en el job diario: ~3 consultas por cronograma activo (~3.000 con 1.000 activos) | Backend | Performance | **Alto** — ✅ Resuelto (`5d6c0ae`) | [MaintenanceScheduleEvaluationJob.cs:75](backend/src/Toner.Infrastructure/Jobs/MaintenanceScheduleEvaluationJob.cs#L75) | **M** |
| 7 | ~15 vistas cargan datos sin `catch`: un fallo de red muestra "no hay registros" en vez de un error | Frontend | Resiliencia | **Alto** — ✅ Resuelto (`883bfa2`) | `views/**` | **S** |
| 8 | Commits parciales: 2–4 `SaveChangesAsync` por operación de negocio, sin transacción explícita. Resuelto por **Tier 1**: colapsar cada operación a un único `SaveChangesAsync` —que EF Core ya envuelve en una transacción implícita— en vez de añadir transacciones explícitas, evitando así el problema del execution strategy de `EnableRetryOnFailure` al reejecutar un delegate cuyo change tracker conserva los cambios del intento fallido. `IAssignmentEngine` pasa a recibir la entidad trackeada en lugar del `Guid` y deja de guardar; se añaden `ServiceTicketService.PrepareStatusChangeAsync` y `MaintenanceOrderService.PrepareCompleteAsync` (mismo patrón que `AssetService.PrepareStatusChangeAsync`). Parte de la solución es el acumulador `_pendingWorkload` del motor de asignación: al desaparecer el commit por asignación, el reparto por carga —que dependía de rebote de ese commit intermedio— habría apilado un lote entero de órdenes sobre el mismo técnico. Los 4 casos quedan en un commit cada uno: `CheckOutAsync` (3→1), job diario (1+N→1), `AddMeterReadingAsync` (2→1), `CreateAsync` (2→1). | Backend | Arquitectura | **Alto** — ✅ Resuelto (`3531c01`) | [AssignmentEngine.cs](backend/src/Toner.Application/Assignment/AssignmentEngine.cs), [TechnicianCheckInService.cs](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs), [MaintenanceScheduleEvaluationJob.cs](backend/src/Toner.Infrastructure/Jobs/MaintenanceScheduleEvaluationJob.cs) | **M** |

### Impacto medio

| # | Hallazgo | Capa | Tipo | Impacto | Archivo | Esfuerzo |
|---|---|---|---|---|---|---|
| 9 | El interceptor RLS añade un round-trip **por consulta**; abrir la conexión una vez por request lo reduce a uno por request | Backend | Performance | Medio — ✅ Resuelto (`b50b2ae`) | [TenantContextInterceptor.cs](backend/src/Toner.Infrastructure/Persistence/TenantContextInterceptor.cs), [Program.cs:331](backend/src/Toner.Api/Program.cs#L331) | **M** |
| 10 | Sin cache en ningún lado; `GET /api/cities` (~1.100 filas inmutables) y el dashboard son candidatos evidentes. Resuelto con `IMemoryCache` nativo a nivel de servicio (sin dependencias nuevas) en 4 endpoints: los 3 catálogos (`cities`, `asset-brands`, `asset-models`) con TTL 4 h y el resumen del dashboard con TTL 30 s, siempre con expiración **absoluta**, no deslizante. `Cities` queda sin invalidación activa porque no existe endpoint de escritura sobre esa tabla; marcas y modelos se invalidan desde sus `Create`/`UpdateAsync`, y los modelos solo para el `brandId` afectado. **La clave del dashboard incluye el tenant**: contra la premisa de que era un agregado global solo-staff, el servicio sí consulta tablas con RLS, y una clave compartida habría sido la fuga de §5.2. Ver §5.3. | Backend | Performance | Medio — ✅ Resuelto (`5a8bc5d`) | [Common/Caching/](backend/src/Toner.Application/Common/Caching/), [CityService.cs](backend/src/Toner.Application/Cities/CityService.cs), [DashboardService.cs](backend/src/Toner.Application/Dashboard/DashboardService.cs) | **S** |
| 11 | Faltan índices en `CreatedAt`, `Status`, `LifecycleStatus`, `ResolvedAt`, `CompletedAt`, `StartTime` y los parciales de `TimeLogs` | DB | Performance | Medio — ✅ Resuelto (`1673d8c`) | Configurations + migración | **S** |
| 12 | `Intl.DateTimeFormat` instanciado en línea dentro de celdas de tabla (hasta 4 por fila) | Frontend | Performance | Medio | [MaintenanceSchedulesView.vue:300](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L300) +5 sitios | **XS** |
| 13 | `.reduce()` anidados dentro del `<template>` para contar elementos de grupo | Frontend | Performance | Medio | [AssetsListView.vue:380](frontend-web/src/views/assets/AssetsListView.vue#L380), [MaintenanceSchedulesView.vue:346](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L346) | **XS** |
| 14 | Listas de DTOs en `ref` profundo donde `shallowRef` bastaría (siempre se reemplazan enteras) | Frontend | Performance | Medio | todas las vistas de listado | **S** |
| 15 | `CheckOutAsync` de ~180 líneas con 3 flujos y 6 dependencias inyectadas | Backend | Arquitectura | Medio | [TechnicianCheckInService.cs:148](backend/src/Toner.Application/Technicians/TechnicianCheckInService.cs#L148) | **M** |
| 16 | `AssetService` (529 líneas) con 4 responsabilidades; la máquina de estados debería estar en Domain | Backend | Arquitectura | Medio | [AssetService.cs](backend/src/Toner.Application/Assets/AssetService.cs) | **M** |
| 17 | `MyWorkView` dispara 7 peticiones en paralelo y las repite entera tras cada acción (6 sitios) | Frontend + Backend | Performance | Medio | [MyWorkView.vue:109](frontend-web/src/views/technicians/MyWorkView.vue#L109) | **M** |
| 18 | `err.response?.data?.title` ×30, sin tipo, descarta los errores por campo y no distingue fallo de red | Frontend | Resiliencia | Medio | `views/**`, [api/http.ts](frontend-web/src/api/http.ts) | **S** |
| 19 | Sin `composables/`: agrupación tri-nivel ×3, `citiesInDepartment` ×3, `statusTagType` ×6 (ya divergieron) | Frontend | Arquitectura | Medio | `views/**`, `components/` | **M** |
| 20 | `Enum.Parse` sobre entrada del usuario → 500 en vez de 400 | Backend | Código limpio | Medio | [AssetService.cs:67](backend/src/Toner.Application/Assets/AssetService.cs#L67) +5 sitios | **XS** |
| 21 | `CurrentUser` (parseo de claims) duplicado literalmente en 5 controllers; `Guid.Parse(...)!` → 500 si falta el claim | Backend | Código limpio | Medio | 5 controllers en `Toner.Api/Controllers/` | **S** |
| 22 | PKs UUIDv4 aleatorias con `ValueGeneratedNever` → fragmentación del btree en las tablas de más escritura | DB | Performance | Medio | [BaseEntity.cs:5](backend/src/Toner.Domain/Common/BaseEntity.cs#L5) | **S** |
| 23 | Sin timeout en axios: una petición colgada deja el spinner girando indefinidamente | Frontend | Resiliencia | Medio | [api/http.ts:5](frontend-web/src/api/http.ts#L5) | **XS** |
| 24 | Excepciones como control de flujo en ~60 sitios para resultados esperados | Backend | Arquitectura | Medio | toda `Toner.Application` | **L** |

### Impacto bajo

| # | Hallazgo | Capa | Tipo | Impacto | Archivo | Esfuerzo |
|---|---|---|---|---|---|---|
| 25 | `.Include()` seguido de `.Select()` — EF lo ignora; 8 sitios de ruido | Backend | Código limpio | Bajo | [UserService.cs:141](backend/src/Toner.Application/Users/UserService.cs#L141) +7 | **XS** |
| 26 | Consulta redundante en `GetByIdAsync`: el DTO ya trae `CurrentClientId` | Backend | Performance | Bajo | [AssetService.cs:135](backend/src/Toner.Application/Assets/AssetService.cs#L135) | **XS** |
| 27 | Tabla `Evidence` con 3 índices y cero código que la use — esquema muerto | DB | Código limpio | Bajo | `EvidenceConfiguration.cs` | **XS** |
| 28 | Cero constraints `CHECK`: nada impide `CounterValue < 0`, `EndDate < StartDate`, umbrales en 0 | DB | Arquitectura | Bajo | Migraciones | **S** |
| 29 | `ToDtoAsync` re-consulta la fila recién escrita en ~20 endpoints (2 consultas en `AssetService`) | Backend | Performance | Bajo | 8 servicios | **M** |
| 30 | `Program.cs` (344 líneas) sin métodos de extensión por capa | Backend | Arquitectura | Bajo | [Program.cs](backend/src/Toner.Api/Program.cs) | **S** |
| 31 | Códigos `"MG"/"MU"/"CI"` y estados como literales crudos compartidos entre backend y frontend | Backend + Frontend | Código limpio | Bajo | [MaintenanceScheduleService.cs:185](backend/src/Toner.Application/Maintenance/MaintenanceScheduleService.cs#L185), `views/**` | **S** |
| 32 | `AttachMaintenanceSummariesAsync` re-consulta cronogramas ya proyectados solo por 5 umbrales | Backend | Performance | Bajo | [MaintenanceScheduleService.cs:149](backend/src/Toner.Application/Maintenance/MaintenanceScheduleService.cs#L149) | **S** |
| 33 | `DashboardView` traga el error real y muestra solo un mensaje genérico, sin log | Frontend | Resiliencia | Bajo | [DashboardView.vue:29](frontend-web/src/views/DashboardView.vue#L29) | **XS** |
| 34 | Sin store de catálogos: ciudades ×3, clientes ×4, marcas ×3 se re-descargan por vista | Frontend | Performance | Bajo | `stores/`, `views/**` | **S** |
| 35 | `auth.user` en `localStorage` nunca se revalida: un cambio de rol no se refleja hasta cerrar sesión | Frontend | Arquitectura | Bajo | [stores/auth.ts](frontend-web/src/stores/auth.ts) | **XS** |
| 36 | Índices de FK "quién hizo qué" nunca usados como predicado (coste de escritura en `MeterReadings`) | DB | Performance | Bajo | Configurations | **XS** |
| 37 | `Azure.Storage.Blobs`, `FirebaseAdmin` y `MailKit` referenciados y sin usar | Backend | Código limpio | Bajo | [Toner.Infrastructure.csproj](backend/src/Toner.Infrastructure/Toner.Infrastructure.csproj) | **XS** |
| 38 | Indexadores de diccionario sobre enums sin guarda de exhaustividad → 500 si se agrega un valor | Backend | Código limpio | Bajo | [AssetService.cs:195](backend/src/Toner.Application/Assets/AssetService.cs#L195) +2 | **XS** |
| 39 | Sin endpoint `/health` para readiness/liveness del contenedor | Backend | Resiliencia | Bajo | [Program.cs](backend/src/Toner.Api/Program.cs) | **XS** |
| 40 | Sin virtualización de tablas (`el-table-v2`); mitigado en su mayor parte por #4 | Frontend | Performance | Bajo | vistas de listado | **M** |
| 41 | Drift respecto a `DESIGN.md` en `MaintenanceSchedulesView.vue`: 2 `font-size: 0.85rem` literales en vez de `var(--text-md)` (el literal está en 13 archivos, pide barrido sistémico) y 8 colores literales fuera de la paleta entre el `<script>` y el `<style>` (los 4 de `urgencyColors`, sus 3 tintes de fila y el `#fff` de `urgencyTagStyle`). **Preexistente** (`883bfa2`), no introducido por ninguna tanda de este audit. Bloqueado por una decisión de diseño pendiente: `DESIGN.md` define dos lámparas de estado (`signal-amber`, `signal-red`) pero el código ya implementa cuatro niveles (`far`/`soon`/`urgent`/`overdue`). Ver §7.8. | Frontend | Cosmético | **Baja** — ⏸️ Pendiente de decisión de Oscar, sin tanda asignada | [MaintenanceSchedulesView.vue:466](frontend-web/src/views/maintenance/MaintenanceSchedulesView.vue#L466) | **XS** (parche local) / **S** (barrido de los 13 archivos) |

### Secuencia sugerida

1. **Ganancias inmediatas (1 día):** #1 (borrar el bucle de iconos), #3, #11, #12, #13, #20, #23, #25, #26, #33, #37, #39. Todo XS, sin riesgo, con efecto medible. **Aplicado y verificado contra Postgres real** (build + suite completa en verde, incluidos los tests de RLS).
2. **Antes de crecer en datos (1 semana):** #5, #6, #7, #10, #22. Es el bloque que evita que el sistema se degrade con el volumen.
3. **Deuda estructural (2–3 semanas):** #2 (rol Postgres con `BYPASSRLS` para staff — el cast por sí solo no alcanza, ver §3.2), #4 (paginación, el más grande y el que más desbloquea), #8 (transacciones), #9 (conexión por request), #15/#16/#19 (descomposición).
4. **Cuando haya espacio:** #24 (Result pattern) y #40 (virtualización) — ambos solo si la medición los justifica después de los anteriores.
