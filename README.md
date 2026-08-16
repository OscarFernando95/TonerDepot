# Toner — Sistema de Gestión de Alquiler y Servicio Técnico de Impresoras

MVP para gestión de alquiler de impresoras (Ricoh), mantenimiento preventivo, soporte correctivo y asignación de técnicos de campo.

## Estructura del repositorio

```
Toner/
├── backend/            # API .NET 8 en capas
│   ├── Toner.sln
│   ├── global.json      # fija el SDK en 8.0.x
│   └── src/
│       ├── Toner.Domain/          # Entidades, enums, sin dependencias externas
│       ├── Toner.Application/     # Casos de uso, DTOs, validación (FluentValidation)
│       ├── Toner.Infrastructure/  # EF Core, migraciones, Hangfire, Blob Storage, email, FCM
│       └── Toner.Api/             # Controllers, JWT, composition root
├── frontend-web/       # Vue 3 + TypeScript + Vite, Element Plus, Pinia, Vue Router
├── mobile/              # Flutter (pendiente)
└── docker-compose.yml   # Postgres + Azurite + smtp4dev
```

## Requisitos

- **.NET SDK 8.0.x** (el repo fija la versión exacta en `backend/global.json`).
- **Docker** y **Docker Compose**, para levantar Postgres, Azurite (Blob Storage local) y smtp4dev (captura de correos en desarrollo).
- Herramienta `dotnet-ef` para migraciones: `dotnet tool install --global dotnet-ef`.

> Si tu máquina tiene más de una instalación de .NET (por ejemplo, la fórmula `dotnet` de Homebrew junto con el cask `dotnet-sdk`), verifica con `dotnet --list-sdks` que el 8.0.x sea visible desde el `dotnet` que se resuelve en tu `PATH`. El `global.json` del backend fuerza el SDK 8.x para este proyecto siempre que esa versión sea accesible.

## Levantar el entorno local

1. Copia la configuración de desarrollo (no está versionada; el `.example` trae valores dummy que ya calzan con el `docker-compose.yml` de este repo):

   ```bash
   cp backend/src/Toner.Api/appsettings.Development.json.example backend/src/Toner.Api/appsettings.Development.json
   ```

2. Levanta la infraestructura (Postgres, Azurite, smtp4dev):

   ```bash
   docker compose up -d
   ```

3. Aplica las migraciones:

   ```bash
   cd backend
   dotnet ef database update \
     --project src/Toner.Infrastructure \
     --startup-project src/Toner.Api
   ```

4. Corre la API:

   ```bash
   dotnet run --project src/Toner.Api
   ```

   Swagger queda disponible en `https://localhost:<puerto>/swagger` en entorno de desarrollo.

## Connection string de desarrollo

Definida en `backend/src/Toner.Api/appsettings.Development.json`, apunta al Postgres del `docker-compose.yml` (usuario/base `toner`, contraseña `toner_dev_password` — solo para desarrollo local, no usar en producción).

## Servicios locales (docker-compose)

| Servicio   | Puerto  | Uso                                              |
|------------|---------|---------------------------------------------------|
| postgres   | 5433 (host) → 5432 (contenedor) | Base de datos principal. Se remapea el puerto host porque 5432 puede estar ocupado por un Postgres nativo instalado en la máquina. |
| azurite    | 10000   | Emulador de Azure Blob Storage (evidencias)         |
| smtp4dev   | 5080 (UI), 2525 (SMTP) | Captura de correos salientes en desarrollo (notificaciones al coordinador). El puerto 5000 se evita porque en macOS lo usa AirPlay Receiver. |

## Jobs programados

Los cronogramas de mantenimiento preventivo y demás tareas periódicas corren sobre **Hangfire**, con almacenamiento en el mismo Postgres (sin dependencia adicional de Redis). El dashboard de Hangfire se expone solo para el rol Administrador (pendiente de cablear).

## Autenticación

Al arrancar, la API siembra automáticamente los 5 roles fijos (`Administrador`, `Coordinador`, `Tecnico`, `Cliente`, `Ventas`) y, si aún no existe ningún Administrador, crea uno de arranque a partir de `AdminBootstrap:Cedula`/`Email`/`Password` en `appsettings.Development.json` (ver `appsettings.Development.json.example` para los nombres de las claves — cada quien define sus propios valores locales, no hay credenciales fijas documentadas aquí). Ese usuario nace con `MustChangePassword = true`, así que el primer login exige cambiarla.

> El volumen de Postgres del `docker-compose.yml` es persistente entre reinicios de contenedor (`docker compose down` sin `-v` no borra los datos). Si ya probaste el login y cambiaste la contraseña del admin de arranque, esa será la vigente hasta que reinicies con una base nueva (`docker compose down -v`).

**Endpoints:**

| Endpoint | Auth | Descripción |
|---|---|---|
| `POST /api/auth/login` | Anónimo | Devuelve JWT + datos del usuario |
| `GET /api/auth/me` | Cualquier usuario autenticado | Perfil del usuario del token actual |
| `POST /api/auth/change-password` | Cualquier usuario autenticado | Cambia la propia contraseña |
| `POST /api/users` | Administrador | Crea un usuario (si `roleName` es `Tecnico`, crea también su perfil `Technician`; si es `Cliente`, requiere `clientId`) |
| `GET /api/users` | Administrador | Lista usuarios |
| `PATCH /api/users/{id}/status` | Administrador | Activa/desactiva un usuario |

Todo endpoint requiere JWT por defecto salvo que tenga `[AllowAnonymous]`. En Swagger, usa el botón "Authorize" pegando el token (sin el prefijo `Bearer `).

> Nota de alcance: el MVP usa un único access token JWT sin refresh token (expira a las `Jwt:ExpiryMinutes`, 8h en desarrollo). Si el uso real de la app móvil de técnicos requiere sesiones más largas o renovación silenciosa, se puede añadir refresh tokens más adelante sin romper el contrato actual.

## Gestión de clientes

Solo accesible para `Administrador` y `Coordinador` (`RoleNames.StaffRoles`).

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/cities` | Staff | Lista ciudades |
| `POST /api/cities` | Administrador | Crea una ciudad (nombre único) |
| `GET /api/clients` | Staff | Lista clientes (incluye conteo de sedes) |
| `GET /api/clients/{id}` | Staff | Detalle de un cliente |
| `POST /api/clients` | Staff | Crea un cliente |
| `PUT /api/clients/{id}` | Staff | Actualiza datos de contacto |
| `PATCH /api/clients/{id}/status` | Staff | Activa/desactiva un cliente |
| `GET /api/clients/{clientId}/locations` | Staff | Lista sedes del cliente |
| `POST /api/clients/{clientId}/locations` | Staff | Crea una sede (requiere `cityId` existente) |
| `PUT /api/clients/{clientId}/locations/{id}` | Staff | Actualiza una sede |
| `PATCH /api/clients/{clientId}/locations/{id}/status` | Staff | Activa/desactiva una sede |
| `GET /api/locations` | Staff | Listado plano de sedes de todos los clientes (para selectores, ej. instalar un activo) |

## Inventario de activos

También solo-Staff. El ciclo de vida (`AssetLifecycleStatus`) es una máquina de estados explícita en `AssetService`, no un campo libre:

```
EnBodega ──────► Instalado ──────► EnMantenimiento
   │                 │  ▲                │
   │                 │  └────────────────┘
   ▼                 ▼
DadoDeBaja       EnBodega
```

`DadoDeBaja` es terminal (ninguna transición sale de ahí). Instalar exige `clientLocationId` la primera vez (viniendo de `EnBodega`); al reinstalar tras un mantenimiento, si no se envía `clientLocationId` se conserva la sede que ya tenía. Cada transición queda registrada en `AssetStatusLog` con quién la hizo y notas opcionales — pasar a `EnBodega` limpia la ubicación actual.

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/asset-brands` | Staff | Lista marcas |
| `POST /api/asset-brands` | Staff | Crea una marca (nombre único) |
| `GET /api/assets` | Staff | Lista activos |
| `GET /api/assets/{id}` | Staff | Detalle de un activo |
| `POST /api/assets` | Staff | Crea un activo (arranca en `EnBodega`) |
| `PUT /api/assets/{id}` | Staff | Actualiza marca/modelo/serie/tipo (no el ciclo de vida) |
| `POST /api/assets/{id}/status` | Staff | Cambia de estado (valida la transición, exige `clientLocationId` cuando corresponde) |
| `GET /api/assets/{id}/status-history` | Staff | Historial de cambios de estado |
| `POST /api/assets/{id}/meter-readings` | Staff | Registra una lectura de contador (rechaza valores menores a la última lectura) |
| `GET /api/assets/{id}/meter-readings` | Staff | Historial de lecturas, más reciente primero |

## Contratos de alquiler

Un `Contract` pertenece a un `Client` y define condiciones (`includedPrintsPerMonth`, `pricePerExtraPage`, vigencia). Los activos se vinculan vía `ContractAsset`, que registra `StartDate`/`EndDate` de la relación — así un activo puede pasar por distintos contratos a lo largo del tiempo sin perder el historial. Regla de negocio: un activo no puede tener dos vínculos de contrato activos (`EndDate == null`) al mismo tiempo; hay que finalizar el vínculo vigente antes de asignarlo a otro contrato.

A diferencia del ciclo de vida de `Asset`, el `Status` del contrato (`Activo`/`Vencido`/`Cancelado`) no tiene una máquina de estados restringida — cualquier transición es válida vía `PATCH`, a criterio del Staff.

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/contracts` | Staff | Lista contratos (incluye nombre del cliente y conteo de activos vinculados activos) |
| `GET /api/contracts/{id}` | Staff | Detalle de un contrato |
| `POST /api/contracts` | Staff | Crea un contrato (arranca en `Activo`) |
| `PUT /api/contracts/{id}` | Staff | Actualiza vigencia/condiciones (no el cliente) |
| `PATCH /api/contracts/{id}/status` | Staff | Cambia el estado |
| `GET /api/contracts/{contractId}/assets` | Staff | Lista activos vinculados (histórico, incluye los ya finalizados) |
| `POST /api/contracts/{contractId}/assets` | Staff | Vincula un activo (falla si ya tiene un vínculo activo en otro contrato) |
| `PATCH /api/contracts/{contractId}/assets/{id}/end` | Staff | Finaliza la vinculación (no la borra, marca `EndDate`) |

## Cronograma de mantenimiento preventivo

Primer job automático del sistema, corriendo sobre **Hangfire** (storage en el mismo Postgres, esquema `hangfire`, sin Redis). `MaintenanceScheduleEvaluationJob` corre diario (`RecurringJob.AddOrUpdate` con `Cron.Daily`) y por cada `MaintenanceSchedule` activo:

1. Si ya hay una `MaintenanceOrder` abierta (no `Completada`/`Cancelada`) para ese cronograma, la salta — evita duplicados.
2. Si es `PorTiempo`, compara `NextDueAt` contra ahora. Si es `PorContador`, compara la última `MeterReading` del activo contra `NextDueCounter`.
3. Si está vencido, crea una `MaintenanceOrder` en `Pendiente`. **No** recalcula `NextDueAt`/`NextDueCounter` en este punto — eso pasa recién al completar la orden, para no adelantar el próximo vencimiento mientras la orden generada sigue sin atenderse.

Al completar una orden (`POST .../complete`), el `MaintenanceSchedule` recalcula: `LastExecutedAt`/`LastExecutedCounter` toman el valor actual, y `NextDueAt`/`NextDueCounter` se recorren un ciclo hacia adelante.

> Alcance: este módulo genera y cierra órdenes, pero **no** asigna técnico ni corre el ciclo de check-in/checklist/evidencias — eso llega con el motor de asignación (módulo 7) y la app móvil (módulo 9), que se conectan sobre la misma `MaintenanceOrder`.

> El dashboard de Hangfire (`/hangfire`) solo se expone en `Development`. No hay puente de autenticación entre el JWT Bearer de la SPA y una navegación de browser directa al dashboard, así que antes de habilitarlo en producción hace falta blindarlo (cookie de sesión de Administrador, o restricción a nivel de red).

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/maintenance-schedules` | Staff | Lista cronogramas |
| `GET /api/maintenance-schedules/{id}` | Staff | Detalle de un cronograma |
| `POST /api/maintenance-schedules` | Staff | Crea un cronograma (calcula el primer vencimiento) |
| `PUT /api/maintenance-schedules/{id}` | Staff | Actualiza el umbral (contador o días) |
| `PATCH /api/maintenance-schedules/{id}/status` | Staff | Activa/pausa el cronograma |
| `GET /api/maintenance-schedules/{id}/orders` | Staff | Historial de órdenes de ese cronograma |
| `POST /api/maintenance-schedules/evaluate-now` | Administrador | Dispara la evaluación manualmente (sin esperar al cron diario) |
| `GET /api/maintenance-orders` | Staff | Lista global de órdenes |
| `GET /api/maintenance-orders/{id}` | Staff | Detalle de una orden |
| `POST /api/maintenance-orders/{id}/complete` | Staff | Completa la orden y recalcula el cronograma |
| `POST /api/maintenance-orders/{id}/cancel` | Staff | Cancela la orden (no recalcula el cronograma) |

## Tickets de soporte correctivo

Primer módulo donde el rol `Cliente` participa directamente: puede reportar y ver **sus propios** tickets (`ClientLocation.ClientId == user.ClientId` del JWT), pero no los de otro cliente. El staff ve y gestiona todos.

Estado del ticket (`ServiceTicketStatus`) con máquina de transiciones explícita, similar a la de `Asset`:

```
Abierto ──assign──► Asignado ──► EnProceso ──► Resuelto ──► Cerrado
   │                    │             │              │
   └──────────────────Cancelado───────┴──────────────┘
                                                  (Resuelto también puede volver a EnProceso)
```

`Asignado` solo se alcanza vía `POST .../assign` (nunca por el `PATCH .../status` genérico), porque esa acción además crea el `AssignmentHistory` con `AssignmentType.Manual`.

> **Alcance original de este módulo**: solo asignación manual. El motor de asignación automático por ciudad/disponibilidad se construyó después, en el módulo 7 (ver más abajo) — la asignación manual sigue disponible para reasignar en cualquier momento. Todavía falta el check-in/check-out del técnico ni carga de evidencias — eso son los módulos 8 y 9, que se conectarán sobre el mismo `AssignmentHistory` y `ServiceTicket`.

Dos ajustes de autorización que salieron de construir esto (documentados porque son fáciles de repetir por error):
- `GET /api/clients/{clientId}/locations` ahora también acepta al rol `Cliente` (antes solo Staff), con verificación de que `clientId` sea el suyo — lo necesita para elegir la sede al reportar un ticket.
- Se agregó `GET /api/technicians` (solo lectura, Staff) porque `GET /api/users` es exclusivo de Administrador y un Coordinador necesita ver la lista de técnicos para asignar tickets.

> **Nota técnica sobre `[Authorize(Roles=)]`**: cuando un controller tiene `[Authorize(Roles=...)]` a nivel de clase y una acción tiene otro a nivel de método, ASP.NET Core **combina ambos con AND** (intersección de roles), no que el de método reemplace al de clase. Esto rompió silenciosamente el acceso de `Cliente` a `ClientLocationsController` la primera vez (clase pedía solo Staff, método pedía Staff+Cliente → la intersección seguía siendo solo Staff). El patrón seguro: o el atributo de método es un *subconjunto* del de clase (para restringir más una acción puntual), o se quita el atributo de clase y cada acción declara su propio conjunto completo.

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/tickets` | Staff (todos) / Cliente (los suyos) | Lista tickets |
| `GET /api/tickets/{id}` | Staff (todos) / Cliente (los suyos) | Detalle de un ticket |
| `POST /api/tickets` | Staff / Cliente (solo su propia sede) | Crea un ticket (arranca en `Abierto`) |
| `POST /api/tickets/{id}/assign` | Staff | Asigna/reasigna técnico, registra `AssignmentHistory` |
| `PATCH /api/tickets/{id}/status` | Staff | Cambia el estado (no permite pasar a `Asignado` directamente) |
| `GET /api/tickets/{id}/assignment-history` | Staff | Historial de asignaciones |
| `GET /api/technicians` | Staff | Directorio de técnicos (solo lectura, para selectores) |

## Motor de asignación de técnicos

`AssignmentEngine` (`IAssignmentEngine`) es el criterio único de asignación automática, usado tanto al crear un `ServiceTicket` como al generar una `MaintenanceOrder` desde el job de mantenimiento — misma lógica, mismo `AssignmentHistory`:

1. Candidatos = técnicos `IsActive`, con `TechnicianCoverage` en la ciudad de la sede (ticket) o de la ubicación actual del activo (orden), y `Status != Ocupado`.
2. Si no hay candidatos: el ticket queda `SinAsignar` (la orden queda `Pendiente` sin técnico) y se registra el intento fallido en `AssignmentHistory` con el motivo.
3. Si hay varios candidatos, gana el de **menor carga de trabajo actual** (tickets `Asignado`/`EnProceso` + órdenes `Asignada`/`EnProceso` que ya tiene asignados). La carga se recalcula en cada asignación, no con una foto tomada al principio del lote — importante cuando el job genera varias órdenes de una vez.

Prerrequisito para que el motor tenga candidatos: cada técnico necesita al menos una `TechnicianCoverage` (ciudad). Se gestiona desde `Técnicos` en el frontend o vía API.

> **Alcance**: la exclusión de `Ocupado` ya es real desde el módulo 8 (check-in/check-out mueve el estado de verdad). Sigue sin haber reintento periódico de tickets `SinAsignar` — se generan una vez, al crear el ticket u orden; si más adelante aparece un técnico disponible, hace falta reasignar manualmente. Y "notifica al coordinador" (de la especificación original) no está implementado — no hay canal de notificaciones (email/push) construido todavía; el coordinador ve los `SinAsignar` navegando la lista de tickets.

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/technicians/{id}/coverage` | Staff | Lista las ciudades de cobertura del técnico |
| `POST /api/technicians/{id}/coverage` | Staff | Agrega una ciudad de cobertura |
| `DELETE /api/technicians/{id}/coverage/{coverageId}` | Staff | Quita una ciudad de cobertura |
| `POST /api/maintenance-orders/{id}/assign` | Staff | Asignación/reasignación manual de una orden, registra `AssignmentHistory` |
| `GET /api/maintenance-orders/{id}/assignment-history` | Staff | Historial de asignaciones de la orden |

## Estado de técnico en tiempo real (check-in / check-out)

`Technician.Status` ahora lo mueve exclusivamente el propio técnico vía check-in/check-out (nunca selección manual, tal como pedía la especificación). Ambas acciones operan sobre "mi" perfil de técnico — se deriva del claim `technician_id` del JWT, nunca de un `{id}` en la URL, así que un técnico solo puede check-in/check-out sobre sí mismo.

- **Check-in** (`POST /api/technicians/me/check-in { serviceTicketId | maintenanceOrderId }`, exactamente uno): valida que el ticket/orden esté asignado a ese técnico y en un estado que admita iniciar trabajo (`Asignado`/`Asignada` o `EnProceso`, para permitir retomar tras una pausa); lo pasa a `EnProceso`, pone `Technician.Status = Ocupado`, abre un `TimeLog` (`StartTime`) y registra el cambio en `TechnicianAvailability`. Rechaza con 409 si el técnico ya está `Ocupado` (no se puede estar en dos visitas a la vez).
- **Check-out** (`POST /api/technicians/me/check-out { resolved = true, notes? }`): cierra el `TimeLog` abierto (`EndTime`), pone `Technician.Status = Disponible`, registra el cambio en `TechnicianAvailability`. Si `resolved = true` (caso por defecto, el flujo feliz de la especificación), además marca el ticket `Resuelto` o completa la orden — **reutilizando** `ServiceTicketService.SetStatusAsync`/`MaintenanceOrderService.CompleteAsync`, no lógica duplicada. Si `resolved = false`, el técnico solo se libera (pausa, fin de turno, falta de repuesto) y el ticket/orden queda en `EnProceso` para retomar después — el dominio ya soportaba esto porque `TimeLog` es una lista, no un campo único: cada check-in/check-out genera una fila nueva.

Esto también es lo que le da efecto real a la exclusión de `Ocupado` en el motor de asignación (módulo 7) — antes de este módulo no había forma de que un técnico llegara a `Ocupado`, así que esa regla estaba escrita pero nunca se ejercitaba con datos reales.

Como consecuencia, `GET /api/tickets` y `GET /api/maintenance-orders` ahora también aceptan al rol `Tecnico`, acotado a lo que tiene asignado (mismo patrón de `RequestingUser` que ya usaba `Cliente`). Esto obligó a otro ajuste de autorización: como `ServiceTicketsController` y `MaintenanceOrdersController` necesitaban un tercer conjunto de roles distinto en algunas acciones (no un subconjunto de lo que ya tenían), les quité el `[Authorize(Roles=)]` de clase y cada acción declara ahora su propio conjunto completo — mismo patrón que ya se había adoptado en `ClientLocationsController` en el módulo 6. Por la misma razón, el autoservicio del técnico vive en un controller aparte (`TechnicianSelfServiceController`) en vez de vivir dentro de `TechniciansController` (que es solo-Staff).

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/technicians/me/status` | Tecnico | Estado propio + ticket/orden activo (si hay) |
| `POST /api/technicians/me/check-in` | Tecnico | Inicia una visita |
| `POST /api/technicians/me/check-out` | Tecnico | Cierra la visita en curso |
| `GET /api/technicians/{id}/time-logs` | Staff | Historial de tiempos de un técnico (auditoría) |

## Portal del cliente

"Levantar solicitudes de servicio" y "ver historial" de tickets ya estaban resueltos desde el módulo 6 (`Cliente` reporta y ve solo sus propios tickets). Lo que faltaba para este módulo era **"ver sus activos"** — y de paso, sus contratos, para completar el "historial" de la relación comercial.

Mismo patrón de scoping que ya se usaba (`RequestingUser` + filtro por `ClientId`), aplicado ahora a `AssetService` y `ContractService`:
- Un `Cliente` en `GET /api/assets` solo ve activos **actualmente instalados en alguna de sus sedes** (`CurrentClientLocation.Client.Id == miClientId`). Un activo en bodega o instalado donde otro cliente no aparece — no es "suyo" todavía.
- Un `Cliente` en `GET /api/contracts` solo ve contratos con `ClientId == miClientId`.
- Todo lo demás de esos dos controllers (crear, editar, cambiar estado, lecturas de contador, historial de estado) sigue siendo exclusivamente Staff — se aplicó el mismo ajuste de quitar el `[Authorize(Roles=)]` de clase y declarar roles explícitos por acción, ya con el patrón bien establecido de módulos anteriores.

> **Alcance**: deliberadamente no se expuso a `Cliente` el historial de `AssetStatusLog` ni `MeterReading` (auditoría interna del ciclo de vida/contador) — eso excede lo que pide un portal "básico". Tampoco hay una vista de "mis sedes" separada; las sedes ya son visibles/seleccionables al reportar un ticket.

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/assets` | Staff (todos) / Cliente (los suyos, instalados) | Lista activos |
| `GET /api/assets/{id}` | Staff (todos) / Cliente (los suyos) | Detalle de un activo |
| `GET /api/contracts` | Staff (todos) / Cliente (los suyos) | Lista contratos |
| `GET /api/contracts/{id}` | Staff (todos) / Cliente (los suyos) | Detalle de un contrato |

## Dashboard de indicadores

Cinco métricas operativas en un solo endpoint compuesto (`GET /api/dashboard/summary?days=N`, Staff únicamente), calculado sobre datos reales de `ServiceTicket`, `MaintenanceOrder` y `TimeLog`:

1. **MTTR** — tiempo medio de resolución (`ResolvedAt - CreatedAt`) de los tickets resueltos dentro del período.
2. **Cumplimiento de SLA** — % de esos mismos tickets resueltos dentro de la meta de su prioridad, y el desglose por prioridad.
3. **Mantenimientos a tiempo** — % de `MaintenanceOrder` completadas dentro del período cuyo `CompletedAt` no superó `ScheduledDate + 3 días`.
4. **Tickets abiertos/sin asignar por ciudad** — backlog actual (no depende del período elegido), agrupado por la ciudad de la sede del ticket.
5. **Utilización de técnicos** — horas registradas en `TimeLog` dentro del período, y esa cifra como % de una base asumida de 8h/día.

> **Decisiones de negocio confirmadas con el usuario** (no derivables del modelo de datos original, así que se preguntó en vez de asumir):
> - Metas de SLA por prioridad: Crítica 4h, Alta 8h, Media 24h, Baja 72h (medidas de `CreatedAt` a `ResolvedAt`).
> - Ventana "a tiempo" de mantenimiento preventivo: `ScheduledDate + 3 días`.
>
> Ambas están como constantes en `DashboardService`, no hardcodeadas en queries — fáciles de mover a configuración si cambian.

> **Alcance**: la utilización de técnicos usa un supuesto propio (base de 8h/día) que **no** fue confirmado como política de negocio — se documenta explícitamente en vez de presentarse como una cifra oficial. El backlog por ciudad es una foto del momento (no filtra por período) porque "abiertos/sin asignar ahora" es lo operacionalmente relevante para un coordinador, a diferencia de MTTR/SLA/mantenimiento que sí tiene sentido acotarlos a una ventana de tiempo. Los cálculos de tiempo se traen con proyecciones mínimas y se agregan en memoria (no en SQL) porque EF Core no traduce de forma confiable aritmética de `TimeSpan` dentro de `Average`/agregaciones.

| Endpoint | Auth | Descripción |
|---|---|---|
| `GET /api/dashboard/summary?days=30` | Administrador, Coordinador | Las 5 métricas para los últimos N días (`days` opcional, por defecto 30) |

## Frontend web (Vue)

Stack: Vue 3 + TypeScript + Vite, [Element Plus](https://element-plus.org) como kit de componentes (tablas, formularios, diálogos), Pinia para estado, Vue Router con guards de autenticación y rol, Axios con interceptor de JWT.

```
frontend-web/
├── .env.development     # VITE_API_URL apuntando a la API local
└── src/
    ├── api/             # cliente axios + un módulo por recurso (auth, users, cities, clients, clientLocations, assetBrands, assets, contracts, contractAssets, maintenanceSchedules, maintenanceOrders, tickets, technicians, dashboard) + types.ts
    ├── stores/auth.ts    # Pinia: token/usuario actual, persistido en localStorage
    ├── router/           # rutas + guard de autenticación y de rol (meta.roles)
    ├── layouts/AppLayout.vue  # sidebar + topbar, menú diferenciado según el rol del usuario
    └── views/             # LoginView, DashboardView, clients/, cities/, users/, assets/, contracts/, maintenance/, tickets/
```

**Levantar:**

```bash
cd frontend-web
npm install
npm run dev
```

Corre en `http://localhost:5173`. Requiere la API corriendo en `http://localhost:5250` (ver `.env.development`) — la API tiene CORS habilitado para ese origen en `appsettings.Development.json` (`Cors:AllowedOrigins`). En producción, agrega el dominio real del frontend a esa misma lista vía configuración/variables de entorno.

**Vistas implementadas**, cubriendo lo que hay construido en el backend hasta ahora (auth + clientes):
- Login
- Dashboard (solo Administrador/Coordinador): las 5 tarjetas/tablas de indicadores con selector de período (7/30/90 días); Cliente y Tecnico ven un saludo simple en su lugar
- Clientes: listar, crear, editar, activar/desactivar
- Detalle de cliente: datos + sedes (listar, crear, editar, activar/desactivar)
- Ciudades: listar, crear (crear es solo-Administrador)
- Marcas: listar, crear
- Activos: listar, crear; detalle con edición de datos, cambio de estado (el selector solo ofrece las transiciones válidas desde el estado actual), historial de estado y lecturas de contador
- Contratos: listar, crear; detalle con edición de condiciones, cambio de estado, y activos vinculados (vincular/finalizar vínculo)
- Cronogramas de mantenimiento: listar, crear, activar/pausar, botón "Evaluar ahora" (solo Administrador) para disparar el job manualmente
- Órdenes de mantenimiento: listar (con técnico asignado); detalle con asignar/reasignar, completar, cancelar e historial de asignación
- Técnicos: listar con su cobertura por ciudad, gestionar cobertura (agregar/quitar ciudades)
- Tickets: listar/crear (Staff y Cliente, cada uno ve/reporta lo que le corresponde; el toast de creación indica si quedó asignado automáticamente y a quién), detalle con asignación manual de técnico e historial (solo Staff)
- Mi trabajo (solo Tecnico): mis tickets/órdenes asignados con botón de check-in; mientras hay una visita en curso, tarjeta de check-out con el switch "¿Quedó terminado?"
- Mis activos / Mis contratos (solo Cliente): lectura simple de lo que tienen instalado y contratado — sin acciones de escritura, esas siguen siendo de Staff
- Usuarios: listar, crear (con perfil `Technician` automático si el rol es Tecnico), activar/desactivar — solo Administrador

El sidebar y las rutas se ajustan según el rol del usuario logueado (`RouteMeta.roles` + `authStore.hasRole(...)`); un usuario sin permiso que fuerza la URL es redirigido al dashboard.

## Estado actual

Módulos completados:
1. **Modelo de datos** — 20 entidades EF Core, migración inicial validada contra Postgres.
2. **Autenticación y roles** — JWT, hashing con BCrypt, seed de roles + admin de arranque, gestión básica de usuarios (crear/listar/activar-desactivar), autorización por rol, manejo centralizado de errores.
3. **Gestión de clientes** — ciudades, clientes, sedes (`ClientLocation`), con relaciones validadas (sede requiere ciudad existente, cliente existente).
4. **Inventario de activos** — marcas, activos, máquina de estados de ciclo de vida (`EnBodega/Instalado/EnMantenimiento/DadoDeBaja`) con historial de cambios (`AssetStatusLog`) y lecturas de contador (`MeterReading`, con validación de que no retrocedan).
5. **Contratos de alquiler** — contratos por cliente con condiciones y vigencia, vínculo con activos (`ContractAsset`, con historial y regla de "un solo vínculo activo por activo").
6. **Cronograma de mantenimiento preventivo** — primer job automático (Hangfire), genera `MaintenanceOrder` por contador o por tiempo, con recálculo del próximo vencimiento al completar.
7. **Tickets de soporte correctivo** — primer módulo con el rol Cliente activo (reporta y ve solo sus propios tickets); asignación manual a técnico con `AssignmentHistory`; máquina de estados `Abierto → Asignado → EnProceso → Resuelto → Cerrado` (+ `Cancelado`).
8. **Motor de asignación de técnicos** — asignación automática por cobertura de ciudad + menor carga de trabajo (excluye técnicos `Ocupado`), usado tanto por `ServiceTicket` al crearse como por `MaintenanceOrder` al generarse; cae a `SinAsignar`/sin técnico y deja registro en `AssignmentHistory` cuando no hay candidato.
9. **Estado de técnico en tiempo real** — check-in/check-out mueve `Technician.Status` de verdad (le da efecto real a la exclusión de `Ocupado` del módulo 7), abre/cierra `TimeLog`, y opcionalmente resuelve el ticket/completa la orden al cerrar la visita.
10. **Portal del cliente** — `Cliente` puede ver sus propios activos instalados y sus contratos (además de reportar/ver tickets, ya cubierto desde el módulo 6); todo de solo lectura, sin tocar las acciones de Staff.
11. **Frontend web** — proyecto Vue conectado a todos los módulos anteriores, con control de acceso por rol replicado en la UI (incluye el selector de estado de activos limitado a transiciones válidas, la vista "Mi trabajo" del técnico, y "Mis activos"/"Mis contratos" del cliente).
12. **Dashboard de indicadores** — MTTR, cumplimiento de SLA (global y por prioridad), % de mantenimientos a tiempo, backlog de tickets abiertos/sin asignar por ciudad, y utilización de técnicos; endpoint compuesto Staff-only con selector de período en la UI.

**Fuera del MVP por ahora, según lo acordado**: la app móvil del técnico (Flutter, módulo 9 original de la lista) queda pendiente — todo lo que necesita (check-in/check-out, mis tickets/órdenes) ya está expuesto por la API y probado desde la versión web de "Mi trabajo".

Ver historial de conversación / commits para el detalle de decisiones de arquitectura.
