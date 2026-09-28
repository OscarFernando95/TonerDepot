import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../models/city.dart';
import '../../models/client.dart';
import '../../models/role_names.dart';
import '../../services/api_client.dart';
import '../../services/city_api.dart';
import '../../services/client_api.dart';
import '../../services/user_api.dart';
import '../../theme/app_theme.dart';

/// Crear usuario — el backend genera la contraseña y la manda por correo
/// cifrado (nunca se acepta una escrita a mano acá); si el rol es Tecnico,
/// crea también el registro de Technician en el mismo paso. La contraseña
/// generada se muestra una sola vez al terminar, en un diálogo.
class UserCreateScreen extends StatefulWidget {
  const UserCreateScreen({super.key});

  @override
  State<UserCreateScreen> createState() => _UserCreateScreenState();
}

class _UserCreateScreenState extends State<UserCreateScreen> {
  final _cedulaController = TextEditingController();
  final _fullNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _addressController = TextEditingController();
  bool _loading = true;
  bool _submitting = false;
  String? _loadError;
  List<City> _cities = [];
  List<Client> _clients = [];
  String? _cityId;
  String _roleName = RoleNames.tecnico;
  String? _clientId;

  @override
  void initState() {
    super.initState();
    _loadOptions();
  }

  Future<void> _loadOptions() async {
    try {
      final results = await Future.wait([
        CityApi(ApiClient.instance).list(),
        ClientApi(ApiClient.instance).list(),
      ]);
      setState(() {
        _cities = results[0] as List<City>;
        _clients = results[1] as List<Client>;
        _loading = false;
      });
    } catch (e) {
      setState(() {
        _loading = false;
        _loadError = e is ApiException
            ? e.message
            : 'No se pudieron cargar las opciones.';
      });
    }
  }

  @override
  void dispose() {
    _cedulaController.dispose();
    _fullNameController.dispose();
    _emailController.dispose();
    _phoneController.dispose();
    _addressController.dispose();
    super.dispose();
  }

  bool get _isValid =>
      _cedulaController.text.trim().isNotEmpty &&
      _fullNameController.text.trim().isNotEmpty &&
      _phoneController.text.trim().isNotEmpty &&
      _addressController.text.trim().isNotEmpty &&
      _cityId != null &&
      (_roleName != RoleNames.cliente || _clientId != null);

  Future<void> _showGeneratedPasswordDialog(String password) async {
    await showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Usuario creado'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Ya se envió por correo, pero esta es la única vez que se muestra en pantalla:',
            ),
            const SizedBox(height: 12),
            SelectableText(
              password,
              style: const TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 18,
              ).merge(AppTextStyles.tabularNumber),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () {
              Clipboard.setData(ClipboardData(text: password));
              ScaffoldMessenger.of(dialogContext).showSnackBar(
                const SnackBar(content: Text('Contraseña copiada.')),
              );
            },
            child: const Text('Copiar'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Listo'),
          ),
        ],
      ),
    );
  }

  Future<void> _submit() async {
    if (!_isValid) return;
    setState(() => _submitting = true);
    try {
      final result = await UserApi(ApiClient.instance).create(
        cedula: _cedulaController.text.trim(),
        email: _emailController.text.trim().isEmpty
            ? null
            : _emailController.text.trim(),
        fullName: _fullNameController.text.trim(),
        phone: _phoneController.text.trim(),
        address: _addressController.text.trim(),
        cityId: _cityId!,
        roleName: _roleName,
        clientId: _roleName == RoleNames.cliente ? _clientId : null,
      );
      if (mounted) await _showGeneratedPasswordDialog(result.generatedPassword);
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiException ? e.message : 'No se pudo crear el usuario.',
            ),
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Nuevo usuario')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _loadError != null
          ? Center(
              child: Text(
                _loadError!,
                style: const TextStyle(color: AppColors.signalRed),
              ),
            )
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                TextField(
                  controller: _cedulaController,
                  keyboardType: TextInputType.number,
                  onChanged: (_) => setState(() {}),
                  decoration: const InputDecoration(labelText: 'Cédula *'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _fullNameController,
                  onChanged: (_) => setState(() {}),
                  decoration: const InputDecoration(
                    labelText: 'Nombre completo *',
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _emailController,
                  keyboardType: TextInputType.emailAddress,
                  decoration: const InputDecoration(
                    labelText: 'Correo (opcional)',
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _phoneController,
                  keyboardType: TextInputType.phone,
                  onChanged: (_) => setState(() {}),
                  decoration: const InputDecoration(labelText: 'Teléfono *'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _addressController,
                  onChanged: (_) => setState(() {}),
                  decoration: const InputDecoration(labelText: 'Dirección *'),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: _cityId,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Ciudad *'),
                  items: [
                    for (final c in _cities)
                      DropdownMenuItem(
                        value: c.id,
                        child: Text(
                          '${c.name} — ${c.stateOrProvince}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (value) => setState(() => _cityId = value),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  initialValue: _roleName,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'Rol'),
                  items: [
                    for (final r in RoleNames.all)
                      DropdownMenuItem(value: r, child: Text(r)),
                  ],
                  onChanged: (value) => setState(() {
                    _roleName = value ?? RoleNames.tecnico;
                    if (_roleName != RoleNames.cliente) _clientId = null;
                  }),
                ),
                if (_roleName == RoleNames.cliente) ...[
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _clientId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Cliente *'),
                    items: [
                      for (final c in _clients)
                        DropdownMenuItem(
                          value: c.id,
                          child: Text(c.name, overflow: TextOverflow.ellipsis),
                        ),
                    ],
                    onChanged: (value) => setState(() => _clientId = value),
                  ),
                ],
                if (_roleName == RoleNames.tecnico)
                  const Padding(
                    padding: EdgeInsets.only(top: 8),
                    child: Text(
                      'Se crea también su registro de técnico, disponible desde el día 1.',
                      style: TextStyle(
                        color: AppColors.inkSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ),
                const SizedBox(height: 24),
                SizedBox(
                  width: double.infinity,
                  height: 52,
                  child: FilledButton(
                    onPressed: _isValid && !_submitting ? _submit : null,
                    child: _submitting
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text(
                            'Crear usuario',
                            style: TextStyle(fontWeight: FontWeight.w700),
                          ),
                  ),
                ),
              ],
            ),
    );
  }
}
