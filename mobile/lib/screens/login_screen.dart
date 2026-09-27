import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../services/cedula_history_store.dart';
import '../state/auth_state.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_surface.dart';
import '../widgets/glass_panel.dart';
import 'server_settings_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  TextEditingController? _cedulaFieldController;
  final _passwordController = TextEditingController();
  bool _submitting = false;
  bool _obscure = true;
  List<String> _cedulaHistory = [];

  @override
  void initState() {
    super.initState();
    CedulaHistoryStore.instance.load().then((history) {
      if (mounted) setState(() => _cedulaHistory = history);
    });
  }

  @override
  void dispose() {
    // El controller del campo de cédula lo crea y libera Autocomplete
    // internamente — no nos toca a nosotros.
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final cedula = (_cedulaFieldController?.text ?? '').trim();
    final password = _passwordController.text;
    if (cedula.isEmpty || password.isEmpty) return;

    setState(() => _submitting = true);
    final auth = context.read<AuthState>();
    final ok = await auth.login(cedula, password);
    if (mounted) setState(() => _submitting = false);
    if (ok) {
      // Solo se recuerda la cédula tras un login exitoso — así la lista de
      // sugerencias no se llena con números mal escritos o inexistentes.
      final updated = await CedulaHistoryStore.instance.remember(cedula);
      if (mounted) setState(() => _cedulaHistory = updated);
    } else if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(auth.lastError ?? 'No se pudo iniciar sesión.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    // Ancho real del campo dentro del ConstrainedBox(maxWidth: 400) más
    // abajo — se reutiliza para que el listado de sugerencias no quede más
    // angosto ni más ancho que el propio campo de cédula.
    final fieldWidth = (MediaQuery.of(context).size.width - 72).clamp(0, 360).toDouble();

    return Scaffold(
      // Sin AppBar: la marca vive dentro de la tarjeta clay, no en una barra
      // gris genérica — el login es la primera impresión de la app y es
      // donde más se nota si el lenguaje visual "glass + clay" está o no.
      body: Stack(
        children: [
          const _LoginBackdrop(),
          SafeArea(
            child: Column(
              children: [
                Align(
                  alignment: Alignment.topRight,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(0, 12, 20, 0),
                    child: _GlassIconButton(
                      icon: Icons.settings_outlined,
                      tooltip: 'Configurar servidor',
                      onPressed: () => Navigator.of(context).push(
                        MaterialPageRoute(builder: (_) => const ServerSettingsScreen()),
                      ),
                    ),
                  ),
                ),
                Expanded(
                  child: Center(
                    child: SingleChildScrollView(
                      padding: const EdgeInsets.all(24),
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 420),
                        child: ClaySurface(
                          radius: 28,
                          padding: const EdgeInsets.fromLTRB(28, 36, 28, 28),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const _BrandBadge(),
                              const SizedBox(height: 20),
                              const Text(
                                'TONER',
                                style: TextStyle(
                                  color: AppColors.inkPrimary,
                                  fontWeight: FontWeight.w800,
                                  fontSize: 28,
                                  letterSpacing: 2,
                                ),
                              ),
                              const SizedBox(height: 6),
                              Text(
                                'Ingresa con tu cédula',
                                style: Theme.of(context)
                                    .textTheme
                                    .bodyMedium
                                    ?.copyWith(color: AppColors.inkSecondary),
                              ),
                              const SizedBox(height: 28),
                              Autocomplete<String>(
                                optionsBuilder: (value) {
                                  if (value.text.isEmpty) return _cedulaHistory;
                                  return _cedulaHistory.where((c) => c.contains(value.text));
                                },
                                fieldViewBuilder: (context, controller, focusNode, onFieldSubmitted) {
                                  // Autocomplete crea este controller una sola vez y lo
                                  // reutiliza en cada rebuild — lo guardamos para que
                                  // _submit() pueda leer el valor final, sin necesidad de
                                  // un controller propio ni listeners.
                                  _cedulaFieldController = controller;
                                  return TextField(
                                    controller: controller,
                                    focusNode: focusNode,
                                    keyboardType: TextInputType.number,
                                    decoration: InputDecoration(
                                      labelText: 'Cédula',
                                      suffixIcon: _cedulaHistory.isEmpty ? null : const Icon(Icons.history, size: 20),
                                    ),
                                    onSubmitted: (_) => onFieldSubmitted(),
                                  );
                                },
                                onSelected: (selection) => _cedulaFieldController?.text = selection,
                                optionsViewBuilder: (context, onSelected, options) {
                                  return Align(
                                    alignment: Alignment.topLeft,
                                    child: Material(
                                      color: AppColors.claySurfaceRaised,
                                      elevation: 6,
                                      shape: RoundedRectangleBorder(
                                        borderRadius: BorderRadius.circular(16),
                                        side: const BorderSide(color: AppColors.neutralSoft),
                                      ),
                                      child: SizedBox(
                                        width: fieldWidth,
                                        child: ConstrainedBox(
                                          constraints: const BoxConstraints(maxHeight: 220),
                                          child: ListView.separated(
                                            padding: EdgeInsets.zero,
                                            shrinkWrap: true,
                                            itemCount: options.length,
                                            separatorBuilder: (_, _) =>
                                                const Divider(height: 1, color: AppColors.neutralSoft),
                                            itemBuilder: (context, index) {
                                              final option = options.elementAt(index);
                                              return ListTile(
                                                dense: true,
                                                leading: const Icon(Icons.history, size: 18, color: AppColors.inkSecondary),
                                                title: Text(option),
                                                onTap: () => onSelected(option),
                                              );
                                            },
                                          ),
                                        ),
                                      ),
                                    ),
                                  );
                                },
                              ),
                              const SizedBox(height: 14),
                              TextField(
                                controller: _passwordController,
                                obscureText: _obscure,
                                onSubmitted: (_) => _submit(),
                                decoration: InputDecoration(
                                  labelText: 'Contraseña',
                                  suffixIcon: IconButton(
                                    icon: Icon(_obscure ? Icons.visibility_off : Icons.visibility),
                                    onPressed: () => setState(() => _obscure = !_obscure),
                                  ),
                                ),
                              ),
                              const SizedBox(height: 24),
                              SizedBox(
                                width: double.infinity,
                                height: 52,
                                child: FilledButton(
                                  onPressed: _submitting ? null : _submit,
                                  child: _submitting
                                      ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                                      : const Text('Ingresar', style: TextStyle(fontWeight: FontWeight.w700)),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Insignia circular de marca: degradé azul + sombra propia, como si la
/// impresora "flotara" sobre la tarjeta — el equivalente clay de un logo.
class _BrandBadge extends StatelessWidget {
  const _BrandBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 84,
      height: 84,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [AppColors.signalBlueBright, AppColors.signalBlue],
        ),
        boxShadow: [
          BoxShadow(
            color: AppColors.signalBlue.withValues(alpha: 0.35),
            offset: const Offset(0, 10),
            blurRadius: 24,
          ),
        ],
      ),
      child: const Icon(Icons.print, size: 40, color: Colors.white),
    );
  }
}

/// Botón circular en vidrio — chrome suelto sobre el fondo, no contenido
/// crítico, así que sí puede ser glass (a diferencia de los campos de abajo).
class _GlassIconButton extends StatelessWidget {
  const _GlassIconButton({required this.icon, required this.onPressed, this.tooltip});

  final IconData icon;
  final VoidCallback onPressed;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final radius = BorderRadius.circular(20);
    return GlassPanel(
      borderRadius: radius,
      child: Material(
        type: MaterialType.transparency,
        child: IconButton(
          icon: Icon(icon, color: AppColors.inkPrimary),
          tooltip: tooltip,
          onPressed: onPressed,
        ),
      ),
    );
  }
}

/// Fondo decorativo: dos manchas de color suaves (azul + ámbar) sobre
/// `canvasBg`, para que la pantalla no se sienta plana y le den algo real al
/// GlassPanel del botón de ajustes para difuminar.
class _LoginBackdrop extends StatelessWidget {
  const _LoginBackdrop();

  @override
  Widget build(BuildContext context) {
    return const IgnorePointer(
      child: Stack(
        children: [
          Positioned(
            top: -140,
            right: -100,
            width: 320,
            height: 320,
            child: DecoratedBox(
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: RadialGradient(colors: [AppColors.signalBlueWash, Colors.transparent]),
              ),
            ),
          ),
          Positioned(
            bottom: -160,
            left: -120,
            width: 320,
            height: 320,
            child: DecoratedBox(
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: RadialGradient(colors: [AppColors.signalAmberWash, Colors.transparent]),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
