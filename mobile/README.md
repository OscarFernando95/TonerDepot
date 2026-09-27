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

- **Flutter SDK.** Si `flutter doctor` no es un comando reconocido, no está
  instalado — en macOS la forma más simple es `brew install --cask flutter`
  (o seguir https://docs.flutter.dev/get-started/install). Después de
  instalar, corre `flutter doctor` y resuelve lo que marque en rojo antes de
  seguir (es normal que Android/iOS marquen cosas la primera vez — ver
  abajo).
- **Un dispositivo o emulador ya abierto** antes de `flutter run` — la app no
  se instala sola en ninguno:
  - *Android*: abre Android Studio → **Device Manager**, crea un dispositivo
    virtual si no tienes uno, y dale ▶ para que arranque. Si `flutter doctor`
    marca el Android toolchain en amarillo/rojo (cmdline-tools faltante,
    licencias sin aceptar), corre `flutter doctor --android-licenses` y
    acepta, o abre Android Studio → SDK Manager → SDK Tools e instala
    "Android SDK Command-line Tools".
  - *iOS* (solo Mac): necesitas Xcode completo (no solo las Command Line
    Tools) instalado desde la App Store, y abrir un simulador desde
    Xcode → Open Developer Tool → Simulator.
  - Corre `flutter devices` para confirmar que el emulador/simulador
    aparece en la lista antes de `flutter run`.
- La API de Toner corriendo localmente (`docker compose up -d` + `dotnet run
  --project backend/src/Toner.Api` desde la raíz del repo, ver README
  principal). El `frontend-web` (`npm run dev` en esa carpeta) no hace falta
  para la app móvil, pero sirve para crear un usuario **Técnico** de prueba
  si aún no tienes uno (Admin → Personal/Usuarios → rol Técnico).

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

## Firma de release (Android)

Sin `android/key.properties`, el build `release` firma con la clave de
debug (Gradle avisa esto en su salida) — sirve para `flutter run --release`
en desarrollo, pero **no debe distribuirse así**. Para firmar con una clave
real:

```bash
keytool -genkey -v -keystore ~/release-key.jks -keyalg RSA -keysize 2048 \
  -validity 10000 -alias toner_tecnico
```

Guarda esa clave y su contraseña en un lugar seguro (no en el repo). Luego
copia `android/key.properties.example` a `android/key.properties` (ya está
en `.gitignore`) y completa `storePassword`, `keyPassword`, `keyAlias` y
`storeFile` con los datos reales.

## Notas de diseño

- El login rechaza explícitamente cualquier rol que no sea `Tecnico` — esta
  app es solo para técnicos de campo, el resto de roles usa `frontend-web`.
- El token JWT se guarda con `flutter_secure_storage` (Keystore en Android,
  Keychain en iOS), no en `SharedPreferences` en texto plano.
- Un 401 del backend (token vencido/inválido) dispara logout automático —
  mismo comportamiento que el interceptor de `http.ts` en el frontend web.
- Las reglas de campos obligatorios del check-out (`CheckoutSheet`) son un
  espejo recortado de `checkoutFormValid` en `MyWorkView.vue`.
