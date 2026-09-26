import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'screens/change_password_screen.dart';
import 'screens/login_screen.dart';
import 'screens/main_shell.dart';
import 'theme/app_theme.dart';
import 'services/api_client.dart';
import 'state/auth_state.dart';
import 'state/meter_reading_state.dart';
import 'state/my_work_state.dart';

void main() {
  runApp(const TonerTecnicoApp());
}

class TonerTecnicoApp extends StatelessWidget {
  const TonerTecnicoApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AuthState(ApiClient.instance)..bootstrap()),
        ChangeNotifierProxyProvider<AuthState, MyWorkState>(
          create: (_) => MyWorkState(ApiClient.instance),
          // MyWorkState no necesita reconstruirse con el AuthState — solo lo
          // usamos como disparador para recargar "Mi trabajo" justo después
          // de un login exitoso (ver AuthGate más abajo).
          update: (_, _, previous) => previous ?? MyWorkState(ApiClient.instance),
        ),
        ChangeNotifierProvider(create: (_) => MeterReadingState(ApiClient.instance)),
      ],
      child: MaterialApp(
        title: 'Toner — Técnico',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.dark,
        themeMode: ThemeMode.dark,
        home: const AuthGate(),
      ),
    );
  }
}

/// Decide qué pantalla mostrar según el estado de sesión — equivalente a los
/// guards de router.beforeEach en el frontend web (router/index.ts), pero
/// mucho más simple porque esta app solo tiene dos destinos.
class AuthGate extends StatelessWidget {
  const AuthGate({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    switch (auth.status) {
      case AuthStatus.unknown:
        return const Scaffold(body: Center(child: CircularProgressIndicator()));
      case AuthStatus.unauthenticated:
        return const LoginScreen();
      case AuthStatus.authenticated:
        // Espejo del guard en router/index.ts (frontend web): mientras el
        // backend siga marcando mustChangePassword, no se entra a MainShell.
        if (auth.currentUser?.mustChangePassword ?? false) {
          return const ChangePasswordScreen();
        }
        return const MainShell();
    }
  }
}
