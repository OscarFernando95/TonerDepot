# Escalado horizontal: tiempo real con más de una instancia (AUDIT.md H-12)

> **Estado: diseño aprobado, no implementado.** Con **una** instancia de App Service no hay que hacer nada: lo de abajo solo es necesario antes de escalar a 2 o más.

## Qué se rompe hoy con 2+ instancias

- `AddSignalR()` sin backplane: cada instancia tiene sus **propias** conexiones WebSocket y sus **propios** grupos en memoria.
- `RealtimeChangeInterceptor` (en `SaveChanges`) llama a `IRealtimeNotifier` → `_hub.Clients.Groups(...)`, que solo alcanza las conexiones **de la instancia que hizo el `SaveChanges`**.
- `RealtimeConnectionTracker` (singleton en memoria) guarda sesión → conexiones → grupos. `SessionsRevokedAsync` lo usa para sacar de sus grupos las conexiones de una sesión revocada y avisarles con `sessionRevoked`. Con 2+ instancias **solo ve las conexiones locales**.

Consecuencias:
- La mitad de los clientes no se entera de los cambios.
- Lo grave: **una sesión revocada sigue recibiendo datos en vivo** si su WebSocket está en la otra instancia.

## Diseño

### 1. Azure SignalR Service (ASRS) como transporte

- `builder.Services.AddSignalR().AddAzureSignalR(...)` (paquete `Microsoft.Azure.SignalR`). Los clientes negocian contra la API y son **redirigidos** a ASRS: las conexiones viven en el servicio, no en las instancias.
- `Clients.Groups(...)`, `Groups.Add/RemoveFromGroupAsync` y `Clients.Client(id)` pasan a ser operaciones del servicio: funcionan **desde cualquier instancia** y sobre **cualquier conexión**.
- **Sin secreto:** ASRS con Managed Identity, igual que Blob Storage (H-18). La cadena de conexión lleva `AuthType=azure.msi` (sin `AccessKey`) y la identidad de App Service recibe el rol **SignalR App Server**.
- **Claims:** al negociar, el servidor emite el token de ASRS con los claims del usuario ya autenticado por nuestro JWT. `UpdatesHub.OnConnectedAsync` sigue decidiendo los grupos desde `Context.User` exactamente igual: qué grupos ve cada uno lo sigue decidiendo el servidor, nunca el cliente.
- **Clientes:** ni la web (`@microsoft/signalr`, `useRealtime.ts`) ni la app (`signalr_netcore`, `realtime_service.dart`) usan `skipNegotiation`, así que ambos negocian y pueden seguir la redirección. `accessTokenFactory` no cambia. **A verificar en un entorno de pruebas** que `signalr_netcore` sigue la redirección de negociación de ASRS antes de activarlo en producción.

### 2. Tracker distribuido de conexiones (revocación de sesión)

ASRS reparte los mensajes, pero para **cortar** una sesión hay que saber qué `connectionId` tiene y en qué grupos está.

- Se extrae una interfaz `IRealtimeConnectionTracker` con dos implementaciones:
  - **`InMemoryRealtimeConnectionTracker`**: la actual. Default para una instancia y para desarrollo.
  - **`PostgresRealtimeConnectionTracker`**: tabla `RealtimeConnections` (`ConnectionId` PK, `SessionId`, `Groups text[]`, `ConnectedAt`).
- `OnConnectedAsync` inserta y `OnDisconnectedAsync` borra.
- `SessionsRevokedAsync` lee las filas de la sesión desde **cualquier** instancia y, vía ASRS, hace `RemoveFromGroupAsync` + `Clients.Client(id).SendAsync("sessionRevoked")`.
- **Filas huérfanas** (instancia caída sin `OnDisconnected`): las retira el job `data-retention` (H-27) por antigüedad. Operar sobre un `connectionId` que ya no existe es inocuo.
- **RLS:** la tabla no tiene `ClientId` ni datos de clientes, solo ids de sesión y de conexión. Requiere una **excepción registrada** en CLAUDE.md, como `UserSessions`.
- **Por qué Postgres y no Redis:** el volumen es bajo (un insert/delete por conexión) y no añade infraestructura ni costo. La interfaz permite cambiar a Redis si algún día se adopta para otra cosa.
- **Alternativa descartada:** avisar solo por el grupo `session:{jti}` sin tracker. Avisa al cliente pero **no** le quita los grupos de datos, así que un cliente modificado seguiría recibiendo información.

### 3. Selección por configuración

- `Realtime:Mode = Local` (default) | `AzureSignalR`.
- `Local` = exactamente el comportamiento actual.
- `AzureSignalR` = `AddAzureSignalR` + `PostgresRealtimeConnectionTracker`.
- El código puede desplegarse antes de escalar y activarse con una variable de entorno.

## Qué cambia **solo** al pasar a más de una instancia

| Pieza | Con 1 instancia (hoy) | Con 2+ instancias |
|---|---|---|
| Transporte SignalR | En proceso | **ASRS obligatorio** (`Realtime:Mode=AzureSignalR`) |
| Tracker de conexiones | En memoria | **Postgres** (tabla + migración) |
| ARR affinity de App Service | Irrelevante | Se puede **apagar** (con ASRS no hace falta) |
| WebSockets en App Service | Encendido | Los clientes van a ASRS; puede quedar encendido |
| Rate limiting (H-13) | Cupo exacto | Cada instancia cuenta por separado: el cupo efectivo es **N ×**. Aceptable al principio; si molesta, partición distribuida (Redis) |
| Caché en memoria (catálogos, resumen del dashboard) | Coherente | La invalidación tras una escritura es **local**: otras instancias sirven datos viejos hasta que expire la entrada. Revisar TTLs o pasar a caché distribuida |
| Hangfire | Un servidor | Varios servidores sobre el mismo Postgres: ya soportado (locks distribuidos, recurrentes sin duplicar) |
| `PasswordEmailOutboxDispatcher` (H-19) | — | `[DisableConcurrentExecution]` ya es distribuido (Hangfire). Sin cambios |
| `CachedHealthCheck` (H-15) | — | Caché por instancia: correcto, cada instancia informa de su propio estado |
| Data Protection | No se usa para auth (JWT) | Sin cambios mientras no haya cookies de auth |

## Coste (orientativo, a confirmar con la calculadora de Azure)

- ASRS **Free**: 20 conexiones y 20.000 mensajes/día. Solo para pruebas.
- ASRS **Standard**: se cobra por unidad (1 unidad ≈ 1.000 conexiones concurrentes). Es lo necesario en producción con 2+ instancias; una unidad cubre de sobra el volumen previsible de técnicos y staff.

## Riesgos y verificación al implementarlo

- **Revocación de sesión (seguridad).** Test obligatorio con dos instancias: revocar desde la instancia A una sesión conectada a la B. El cliente debe recibir `sessionRevoked` y **dejar de recibir** `entityChanged`.
- **App móvil:** comprobar la redirección de negociación en un entorno de pruebas.
- **Tabla nueva sin RLS:** registrar la excepción en CLAUDE.md, con su justificación.
