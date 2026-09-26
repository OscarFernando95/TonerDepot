import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../services/cedula_history_store.dart';
import '../state/auth_state.dart';
import '../theme/app_theme.dart';
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
    final fieldWidth = (MediaQuery.of(context).size.width - 48).clamp(0, 400).toDouble();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Toner — Técnico'),
        actions: [
          IconButton(
            icon: const Icon(Icons.settings_outlined),
            tooltip: 'Configurar servidor',
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const ServerSettingsScreen()),
            ),
          ),
        ],
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 400),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.print, size: 64, color: AppColors.signalBlueBright),
                const SizedBox(height: 8),
                Text('Ingresa con tu cédula', style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 24),
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
                        color: AppColors.boardPanelRaised,
                        elevation: 6,
                        shape: const RoundedRectangleBorder(side: BorderSide(color: AppColors.boardSeamSoft)),
                        child: SizedBox(
                          width: fieldWidth,
                          child: ConstrainedBox(
                            constraints: const BoxConstraints(maxHeight: 220),
                            child: ListView.separated(
                              padding: EdgeInsets.zero,
                              shrinkWrap: true,
                              itemCount: options.length,
                              separatorBuilder: (_, _) => const Divider(height: 1, color: AppColors.boardSeamSoft),
                              itemBuilder: (context, index) {
                                final option = options.elementAt(index);
                                return ListTile(
                                  dense: true,
                                  leading: const Icon(Icons.history, size: 18, color: AppColors.flapInkDim),
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
                const SizedBox(height: 12),
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
                const SizedBox(height: 20),
                SizedBox(
                  width: double.infinity,
                  height: 48,
                  child: FilledButton(
                    onPressed: _submitting ? null : _submit,
                    child: _submitting
                        ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                        : const Text('Ingresar'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
