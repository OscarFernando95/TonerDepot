import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/auth_state.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_surface.dart';

/// Pantalla forzosa de cambio de contraseña — equivalente a
/// ChangePasswordView.vue + el guard en router/index.ts del frontend web.
/// El `redirect` de app_router.dart la muestra en vez de AppShell mientras
/// currentUser.mustChangePassword sea true; el backend refuerza lo mismo del
/// lado servidor (MustChangePasswordMiddleware), esto es solo la parte de UX.
class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _currentController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();
  bool _obscureCurrent = true;
  bool _obscureNew = true;
  bool _submitting = false;
  String? _error;

  // Mismas reglas que ChangePasswordRequestValidator (backend) y
  // usePasswordRules.ts (frontend web): mínimo 10 caracteres, al menos una
  // mayúscula, una minúscula y un dígito. Sin esto duplicado aquí, el
  // técnico solo se entera de qué falta hasta que el servidor lo rechaza.
  static const _minLength = 10;

  bool get _hasMinLength => _newController.text.length >= _minLength;
  bool get _hasUpper => RegExp('[A-Z]').hasMatch(_newController.text);
  bool get _hasLower => RegExp('[a-z]').hasMatch(_newController.text);
  bool get _hasDigit => RegExp('[0-9]').hasMatch(_newController.text);
  bool get _matches => _newController.text.isNotEmpty && _newController.text == _confirmController.text;

  bool get _isValid =>
      _currentController.text.isNotEmpty && _hasMinLength && _hasUpper && _hasLower && _hasDigit && _matches;

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_isValid) return;
    setState(() {
      _submitting = true;
      _error = null;
    });
    final auth = context.read<AuthState>();
    final error = await auth.changePassword(_currentController.text, _newController.text);
    if (!mounted) return;
    setState(() => _submitting = false);
    if (error != null) {
      setState(() => _error = error);
    }
    // Si no hay error, AuthState ya puso mustChangePassword en false y
    // notificó — el `redirect` de app_router.dart reacciona solo y navega a
    // /dashboard dentro de AppShell.
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    return Scaffold(
      appBar: AppBar(
        title: const Text('Cambiar contraseña'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Cerrar sesión',
            onPressed: () => auth.logout(),
          ),
        ],
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: ClaySurface(
              radius: 26,
              padding: const EdgeInsets.fromLTRB(24, 32, 24, 24),
              child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Center(
                  child: Container(
                    width: 72,
                    height: 72,
                    decoration: const BoxDecoration(shape: BoxShape.circle, color: AppColors.signalAmberWash),
                    child: const Icon(Icons.lock_outline, size: 32, color: AppColors.signalAmber),
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  'Debes cambiar tu contraseña antes de continuar.',
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 24),
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 16),
                    child: ClaySurface(
                      color: AppColors.signalRedWash,
                      borderColor: AppColors.signalRed,
                      radius: 16,
                      padding: const EdgeInsets.all(12),
                      child: Text(_error!, style: const TextStyle(color: AppColors.signalRed)),
                    ),
                  ),
                TextField(
                  controller: _currentController,
                  obscureText: _obscureCurrent,
                  decoration: InputDecoration(
                    labelText: 'Contraseña actual',
                    suffixIcon: IconButton(
                      icon: Icon(_obscureCurrent ? Icons.visibility_off : Icons.visibility),
                      onPressed: () => setState(() => _obscureCurrent = !_obscureCurrent),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _newController,
                  obscureText: _obscureNew,
                  onChanged: (_) => setState(() {}),
                  decoration: InputDecoration(
                    labelText: 'Nueva contraseña',
                    suffixIcon: IconButton(
                      icon: Icon(_obscureNew ? Icons.visibility_off : Icons.visibility),
                      onPressed: () => setState(() => _obscureNew = !_obscureNew),
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                _RuleRow(met: _hasMinLength, label: 'Al menos $_minLength caracteres'),
                _RuleRow(met: _hasUpper, label: 'Una letra mayúscula'),
                _RuleRow(met: _hasLower, label: 'Una letra minúscula'),
                _RuleRow(met: _hasDigit, label: 'Un número'),
                const SizedBox(height: 12),
                TextField(
                  controller: _confirmController,
                  obscureText: _obscureNew,
                  onChanged: (_) => setState(() {}),
                  onSubmitted: (_) => _submit(),
                  decoration: const InputDecoration(labelText: 'Confirmar nueva contraseña'),
                ),
                const SizedBox(height: 20),
                SizedBox(
                  height: 48,
                  child: FilledButton(
                    onPressed: _isValid && !_submitting ? _submit : null,
                    child: _submitting
                        ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('Cambiar contraseña'),
                  ),
                ),
              ],
            ),
            ),
          ),
        ),
      ),
    );
  }
}

class _RuleRow extends StatelessWidget {
  const _RuleRow({required this.met, required this.label});

  final bool met;
  final String label;

  @override
  Widget build(BuildContext context) {
    final color = met ? AppColors.signalBlueBright : AppColors.inkSecondary;
    return Padding(
      padding: const EdgeInsets.only(top: 2),
      child: Row(
        children: [
          Icon(met ? Icons.check_circle : Icons.circle_outlined, size: 14, color: color),
          const SizedBox(width: 6),
          Text(label, style: TextStyle(color: color, fontSize: 12)),
        ],
      ),
    );
  }
}
