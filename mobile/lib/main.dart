import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'router/app_router.dart';
import 'theme/app_theme.dart';
import 'services/api_client.dart';
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

  @override
  void initState() {
    super.initState();
    _authState.bootstrap();
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
