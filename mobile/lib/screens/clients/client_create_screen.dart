import 'package:flutter/material.dart';
import 'package:latlong2/latlong.dart';

import '../../models/city.dart';
import '../common/location_picker_screen.dart';
import '../../services/api_client.dart';
import '../../services/city_api.dart';
import '../../services/client_api.dart';
import '../../theme/app_theme.dart';
import '../../utils/phone_input.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/department_city_picker.dart';

/// Crear cliente — el backend exige al menos una sede (locations no puede
/// venir vacío, ver CreateClientRequestValidator), por eso el form arranca
/// con una fila de sede y permite agregar más.
class ClientCreateScreen extends StatefulWidget {
  const ClientCreateScreen({super.key});

  @override
  State<ClientCreateScreen> createState() => _ClientCreateScreenState();
}

class _LocationDraft {
  final nameController = TextEditingController();
  final addressController = TextEditingController();
  final contactNameController = TextEditingController();
  final contactPhoneController = TextEditingController();
  /// Cascada Departamento→Ciudad: es local al formulario, nunca se manda al
  /// backend (mismo comentario que CreateClientDialog.vue/UsersView.vue).
  String? departmentName;
  String? cityId;
  double? latitude;
  double? longitude;

  void dispose() {
    nameController.dispose();
    addressController.dispose();
    contactNameController.dispose();
    contactPhoneController.dispose();
  }
}

class _ClientCreateScreenState extends State<ClientCreateScreen> {
  final _nameController = TextEditingController();
  final _taxIdController = TextEditingController();
  final _contactNameController = TextEditingController();
  final _contactEmailController = TextEditingController();
  final _contactPhoneController = TextEditingController();
  bool _isContractClient = true;
  String _supportCoverage = 'HorarioOficina';
  bool _submitting = false;
  bool _loadingCities = true;
  String? _loadError;
  List<City> _cities = [];
  final List<_LocationDraft> _locations = [_LocationDraft()];

  @override
  void initState() {
    super.initState();
    _loadCities();
  }

  Future<void> _loadCities() async {
    try {
      final cities = await CityApi(ApiClient.instance).list();
      setState(() {
        _cities = cities;
        _loadingCities = false;
      });
    } catch (e) {
      setState(() {
        _loadingCities = false;
        _loadError = e is ApiException
            ? e.message
            : 'No se pudieron cargar las ciudades.';
      });
    }
  }

  @override
  void dispose() {
    _nameController.dispose();
    _taxIdController.dispose();
    _contactNameController.dispose();
    _contactEmailController.dispose();
    _contactPhoneController.dispose();
    for (final loc in _locations) {
      loc.dispose();
    }
    super.dispose();
  }

  bool get _isValid =>
      _nameController.text.trim().isNotEmpty &&
      isValidPhone(_contactPhoneController.text) &&
      _locations.every(
        (l) =>
            l.cityId != null &&
            l.nameController.text.trim().isNotEmpty &&
            l.addressController.text.trim().isNotEmpty &&
            isValidPhone(l.contactPhoneController.text),
      );

  Future<void> _submit() async {
    if (!_isValid) return;
    setState(() => _submitting = true);
    try {
      final created = await ClientApi(ApiClient.instance).create(
        name: _nameController.text.trim(),
        taxId: _taxIdController.text.trim().isEmpty
            ? null
            : _taxIdController.text.trim(),
        contactName: _contactNameController.text.trim().isEmpty
            ? null
            : _contactNameController.text.trim(),
        contactEmail: _contactEmailController.text.trim().isEmpty
            ? null
            : _contactEmailController.text.trim(),
        contactPhone: _contactPhoneController.text.trim().isEmpty
            ? null
            : _contactPhoneController.text.trim(),
        isContractClient: _isContractClient,
        supportCoverage: _supportCoverage,
        locations: [
          for (final loc in _locations)
            (
              cityId: loc.cityId!,
              name: loc.nameController.text.trim(),
              address: loc.addressController.text.trim(),
              contactName: loc.contactNameController.text.trim().isEmpty
                  ? null
                  : loc.contactNameController.text.trim(),
              contactPhone: loc.contactPhoneController.text.trim().isEmpty
                  ? null
                  : loc.contactPhoneController.text.trim(),
              latitude: loc.latitude,
              longitude: loc.longitude,
            ),
        ],
      );
      if (mounted) Navigator.of(context).pop(created);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              e is ApiException ? e.message : 'No se pudo crear el cliente.',
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
      appBar: AppBar(title: const Text('Nuevo cliente')),
      body: _loadingCities
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
                ClaySurface(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Datos del cliente',
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _nameController,
                        onChanged: (_) => setState(() {}),
                        decoration: const InputDecoration(
                          labelText: 'Nombre *',
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _taxIdController,
                        decoration: const InputDecoration(
                          labelText: 'NIT (opcional)',
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _contactNameController,
                        decoration: const InputDecoration(
                          labelText: 'Contacto (opcional)',
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _contactEmailController,
                        keyboardType: TextInputType.emailAddress,
                        decoration: const InputDecoration(
                          labelText: 'Correo de contacto (opcional)',
                        ),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _contactPhoneController,
                        keyboardType: TextInputType.phone,
                        inputFormatters: phoneInputFormatters,
                        onChanged: (_) => setState(() {}),
                        decoration: InputDecoration(
                          labelText: 'Teléfono de contacto (opcional)',
                          errorText: phoneErrorText(_contactPhoneController.text),
                        ),
                      ),
                      const SizedBox(height: 8),
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        title: const Text('Cliente con contrato'),
                        subtitle: const Text(
                          'Apágalo si es un cliente externo sin equipos catalogados',
                          style: TextStyle(fontSize: 12),
                        ),
                        value: _isContractClient,
                        onChanged: (v) => setState(() => _isContractClient = v),
                      ),
                      const SizedBox(height: 4),
                      const Text('Cobertura de soporte'),
                      const SizedBox(height: 4),
                      SegmentedButton<String>(
                        segments: const [
                          ButtonSegment(
                            value: 'HorarioOficina',
                            label: Text('Horario de oficina'),
                          ),
                          ButtonSegment(
                            value: 'Continuo24x7',
                            label: Text('24/7'),
                          ),
                        ],
                        selected: {_supportCoverage},
                        onSelectionChanged: (v) =>
                            setState(() => _supportCoverage = v.first),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  'Sedes iniciales',
                  style: Theme.of(context).textTheme.titleSmall,
                ),
                const SizedBox(height: 8),
                for (int i = 0; i < _locations.length; i++)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: _LocationForm(
                      draft: _locations[i],
                      cities: _cities,
                      onChanged: () => setState(() {}),
                      onRemove: _locations.length > 1
                          ? () => setState(() => _locations.removeAt(i))
                          : null,
                    ),
                  ),
                OutlinedButton.icon(
                  onPressed: () =>
                      setState(() => _locations.add(_LocationDraft())),
                  icon: const Icon(Icons.add_location_alt_outlined, size: 18),
                  label: const Text('Agregar otra sede'),
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
                            'Crear cliente',
                            style: TextStyle(fontWeight: FontWeight.w700),
                          ),
                  ),
                ),
              ],
            ),
    );
  }
}

class _LocationForm extends StatelessWidget {
  const _LocationForm({
    required this.draft,
    required this.cities,
    required this.onChanged,
    this.onRemove,
  });

  final _LocationDraft draft;
  final List<City> cities;
  final VoidCallback onChanged;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Expanded(
                child: Text(
                  'Sede',
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
              if (onRemove != null)
                IconButton(
                  icon: const Icon(
                    Icons.delete_outline,
                    color: AppColors.signalRed,
                  ),
                  onPressed: onRemove,
                ),
            ],
          ),
          DepartmentCityPicker(
            cities: cities,
            initialDepartmentName: draft.departmentName,
            initialCityId: draft.cityId,
            onChanged: (departmentName, cityId) {
              draft.departmentName = departmentName;
              draft.cityId = cityId;
              onChanged();
            },
          ),
          const SizedBox(height: 12),
          TextField(
            controller: draft.nameController,
            onChanged: (_) => onChanged(),
            decoration: const InputDecoration(labelText: 'Nombre de la sede *'),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: draft.addressController,
            onChanged: (_) => onChanged(),
            decoration: const InputDecoration(labelText: 'Dirección *'),
          ),
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: () async {
                final lat = draft.latitude;
                final lon = draft.longitude;
                final picked = await Navigator.of(context).push<LatLng>(
                  MaterialPageRoute(
                    builder: (_) => LocationPickerScreen(
                      initial: lat != null && lon != null ? LatLng(lat, lon) : null,
                      searchHint: draft.addressController.text.trim(),
                    ),
                  ),
                );
                if (picked == null) return;
                draft.latitude = picked.latitude;
                draft.longitude = picked.longitude;
                onChanged();
              },
              icon: const Icon(Icons.map_outlined, size: 18),
              label: Text(
                draft.latitude == null
                    ? 'Elegir ubicación en el mapa'
                    : 'Ubicación: ${draft.latitude!.toStringAsFixed(5)}, ${draft.longitude!.toStringAsFixed(5)}',
              ),
            ),
          ),
          const SizedBox(height: 4),
          TextField(
            controller: draft.contactNameController,
            decoration: const InputDecoration(labelText: 'Contacto (opcional)'),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: draft.contactPhoneController,
            keyboardType: TextInputType.phone,
            inputFormatters: phoneInputFormatters,
            onChanged: (_) => onChanged(),
            decoration: InputDecoration(
              labelText: 'Teléfono (opcional)',
              errorText: phoneErrorText(draft.contactPhoneController.text),
            ),
          ),
        ],
      ),
    );
  }
}
