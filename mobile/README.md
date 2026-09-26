# Toner — App del Técnico (Flutter)

Primera versión: login y "Mi trabajo" para el rol **Técnico**, sobre los mismos
endpoints que ya usa `frontend-web` (`/auth/*`, `/technicians/me/*`, `/tickets`,
`/maintenance-orders`). Cubre login, ver tickets/órdenes asignadas, check-in y
check-out.

**Fuera de esta primera versión** (quedan para una siguiente iteración):
coverage (trabajo de otros técnicos en tu ciudad), tomar tickets/órdenes libres
(`claim`), e instalación de activos (`pending-installations`). El backend ya
expone todo eso — solo falta la pantalla.

## Requisitos

- Flutter SDK (el mismo que ya usas para GesGan).
- La API de Toner corriendo localmente (`docker compose up -d` + `dotnet run
  --project backend/src/Toner.Api` desde la raíz del repo, ver README
  principal).

## Configurar la URL de la API

La API de desarrollo no tiene HTTPS ni un dominio fijo, así que la URL depende
de dónde corras la app:

| Dónde corre la app | URL a usar |
|---|---|
| Emulador de Android | `http://10.0.2.2:5250/api` (ya es el valor por defecto) |
| Simulador de iOS | `http://localhost:5250/api` (ya es el valor por defecto) |
| Celular físico (misma red Wi-Fi que tu PC) | `http://<IP-LAN-de-tu-PC>:5250/api` |

Para un celular físico, cambia la URL desde la app: en la pantalla de login,
toca el ícono de engranaje (⚙) y pega la URL. Se guarda de forma persistente
(no hay que volver a compilar).

## Correr

```bash
cd mobile
flutter pub get
flutter run
```

## Estructura

```
lib/
├── config/        # AppConfig: URL por defecto según plataforma
├── models/        # DTOs (espejo de frontend-web/src/api/types.ts)
├── services/      # ApiClient (Dio + JWT + manejo de errores) + un cliente por recurso
├── state/         # AuthState y MyWorkState (ChangeNotifier, vía package:provider)
├── screens/       # LoginScreen, MyWorkScreen, ServerSettingsScreen
└── widgets/       # TicketCard, OrderCard, CheckoutSheet
```

## Notas de diseño

- El login rechaza explícitamente cualquier rol que no sea `Tecnico` — esta
  app es solo para técnicos de campo, el resto de roles usa `frontend-web`.
- El token JWT se guarda con `flutter_secure_storage` (Keystore en Android,
  Keychain en iOS), no en `SharedPreferences` en texto plano.
- Un 401 del backend (token vencido/inválido) dispara logout automático —
  mismo comportamiento que el interceptor de `http.ts` en el frontend web.
- Las reglas de campos obligatorios del check-out (`CheckoutSheet`) son un
  espejo recortado de `checkoutFormValid` en `MyWorkView.vue`.
