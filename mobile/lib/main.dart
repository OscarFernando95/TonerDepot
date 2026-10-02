import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'router/app_router.dart';
import 'theme/app_theme.dart';
import 'services/api_client.dart';
import 'services/push_service.dart';
import 'state/auth_state.dart';
import 'state/meter_reading_state.dart';
import 'state/my_work_state.dart';

void main() {
  runApp(const TonerApp());
}

class TonerApp extends StatefulWidget {
  const TonerApp({super.key});

  @override
  State<TonerApp> createState() => _TonerAppState();
}

class _TonerAppState extends State<TonerApp> {
  final _authState = AuthState(ApiClient.instance);
  late final _router = buildAppRouter(_authState);
  // Pantalla a la que lleva la notificación que el usuario tocó; espera a que haya sesión y no toque cambiar la
  // contraseña (una notificación con la sesión cerrada abre el login y, al entrar, llega aquí).
  String? _pendingPushPath;

  @override
  void initState() {
    super.initState();
    _authState.addListener(_onAuthChanged);
    _authState.bootstrap();
    PushService.instance.init(
      onOpen: (path) {
        _pendingPushPath = path;
        _openPendingPush();
      },
    );
  }

  @override
  void dispose() {
    _authState.removeListener(_onAuthChanged);
    super.dispose();
  }

  bool get _readyForPush =>
      _authState.status == AuthStatus.authenticated &&
      _authState.currentUser?.mustChangePassword == false;

  void _onAuthChanged() {
    if (!_readyForPush) return;
    // Asocia el dispositivo al usuario que acaba de entrar (el token no se borra al cerrar sesión).
    PushService.instance.registerToken();
    _openPendingPush();
  }

  void _openPendingPush() {
    final path = _pendingPushPath;
    if (path == null || !_readyForPush) return;
    _pendingPushPath = null;
    // Después del frame: el redirect del router (login → inicio) ya ocurrió y la pantalla se apila encima.
    WidgetsBinding.instance.addPostFrameCallback((_) => _router.push(path));
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: _authState),
        ChangeNotifierProxyProvider<AuthState, MyWorkState>(
          create: (_) => MyWorkState(ApiClient.instance),
          // MyWorkState no necesita reconstruirse con el AuthState — solo lo
          // usamos como disparador para recargar "Mi trabajo" justo después
          // de un login exitoso (ver AppShell.initState).
          update: (_, _, previous) =>
              previous ?? MyWorkState(ApiClient.instance),
        ),
        ChangeNotifierProvider(
          create: (_) => MeterReadingState(ApiClient.instance),
        ),
      ],
      child: MaterialApp.router(
        title: 'Toner',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light,
        themeMode: ThemeMode.light,
        routerConfig: _router,
      ),
    );
  }
}
