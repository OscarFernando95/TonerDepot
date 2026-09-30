import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/client.dart';
import '../../models/client_location.dart';
import '../../models/paged_result.dart';
import '../../models/role_names.dart';
import '../../services/api_client.dart';
import '../../services/asset_api.dart';
import '../../services/client_api.dart';
import '../../services/client_location_api.dart';
import '../../services/ticket_api.dart';
import '../../state/auth_state.dart';
import '../../theme/app_theme.dart';

/// Formulario de creación de ticket. Cliente reporta sobre su propia sede
/// (sedes/activos ya filtrados por el backend). Staff (Administrador/
/// Coordinador) elige cliente → sede → activo — espejo de
/// TicketsListView.vue, con un botón "+" para crear el cliente sin salir del
/// flujo (empuja a /clients/new y selecciona el que vuelva). Opaco/clay a
/// propósito, no glass — es un formulario de datos reales (igual criterio
/// que checkout_sheet.dart), no chrome decorativo.
class TicketCreateSheet extends StatefulWidget {
  const TicketCreateSheet({super.key});

  static Future<bool?> show(BuildContext context) {
    return showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.only(topLeft: Radius.circular(24), topRight: Radius.circular(24)),
      ),
      builder: (_) => const TicketCreateSheet(),
    );
  }

  @override
  State<TicketCreateSheet> createState() => _TicketCreateSheetState();
}

class _TicketCreateSheetState extends State<TicketCreateSheet> {
  static const _priorities = ['Baja', 'Media', 'Alta', 'Critica'];

  final _descriptionController = TextEditingController();
  bool _loadingOptions = true;
  bool _submitting = false;
  String? _loadError;
  bool _isStaff = false;

  // Cliente propio (flujo Cliente).
  List<ClientLocation> _locations = [];
  List<Asset> _assets = [];

  // Solo flujo Staff.
  List<Client> _clients = [];
  String? _selectedClientId;
  bool _selectedClientIsContract = true;

  String? _selectedLocationId;
  String? _selectedArea;
  String? _selectedAssetId;
  String _priority = 'Media';

  List<Asset> get _assetsAtSelectedLocation =>
      _assets.where((a) => a.currentClientLocationId == _selectedLocationId).toList();

  // Área es texto libre (Asset.Area) capturado al instalar el equipo — no
  // hay catálogo de áreas en el backend, así que se deriva de las áreas ya
  // usadas por los activos de la sede elegida (ver AssetService al hacer
  // ListForMeterReadingAsync/ListAsync, mismo campo). Solo aplica al flujo
  // Cliente — el formulario de Staff mantiene el mismo alcance que
  // TicketsListView.vue (sin filtro de área).
  List<String> get _areasAtSelectedLocation {
    final areas = _assetsAtSelectedLocation
        .map((a) => a.area)
        .whereType<String>()
        .where((a) => a.isNotEmpty)
        .toSet()
        .toList();
    areas.sort();
    return areas;
  }

  List<Asset> get _assetsAtSelectedLocationAndArea => _selectedArea == null
      ? _assetsAtSelectedLocation
      : _assetsAtSelectedLocation.where((a) => a.area == _selectedArea).toList();

  @override
  void initState() {
    super.initState();
    _loadOptions();
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _loadOptions() async {
    final auth = context.read<AuthState>();
    final clientId = auth.currentUser?.clientId;
    _isStaff = auth.hasAnyRole(RoleNames.staffRoles);

    if (clientId != null) {
      try {
        final client = ApiClient.instance;
        final results = await Future.wait([
          ClientLocationApi(client).listForClient(clientId),
          AssetApi(client).listMine(),
        ]);
        setState(() {
          _locations = results[0] as List<ClientLocation>;
          _assets = results[1] as List<Asset>;
          _loadingOptions = false;
        });
      } catch (e) {
        setState(() {
          _loadingOptions = false;
          _loadError = e is ApiException ? e.message : 'No se pudieron cargar tus sedes.';
        });
      }
      return;
    }

    if (!_isStaff) {
      setState(() {
        _loadingOptions = false;
        _loadError = 'Tu usuario no tiene un cliente asociado.';
      });
      return;
    }

    try {
      final client = ApiClient.instance;
      final results = await Future.wait([
        ClientApi(client).list(),
        AssetApi(client).listCatalog(page: 1, pageSize: 200),
      ]);
      setState(() {
        _clients = results[0] as List<Client>;
        _assets = (results[1] as PagedResult<Asset>).items;
        _loadingOptions = false;
      });
    } catch (e) {
      setState(() {
        _loadingOptions = false;
        _loadError = e is ApiException ? e.message : 'No se pudieron cargar los clientes.';
      });
    }
  }

  Future<void> _onClientChanged(String? clientId) async {
    setState(() {
      _selectedClientId = clientId;
      _selectedClientIsContract = _clients.firstWhereOrNull((c) => c.id == clientId)?.isContractClient ?? true;
      _selectedLocationId = null;
      _selectedAssetId = null;
      _locations = [];
    });
    if (clientId == null) return;
    try {
      final locations = await ClientLocationApi(ApiClient.instance).listForClient(clientId);
      if (mounted) setState(() => _locations = locations);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e is ApiException ? e.message : 'No se pudieron cargar las sedes.')),
        );
      }
    }
  }

  Future<void> _createClient() async {
    final created = await context.push<Client>('/clients/new');
    if (created != null && mounted) {
      setState(() => _clients = [..._clients, created]);
      await _onClientChanged(created.id);
    }
  }

  Future<void> _submit() async {
    if (_selectedLocationId == null || _descriptionController.text.trim().isEmpty) return;
    if (_isStaff && _selectedClientId == null) return;
    setState(() => _submitting = true);
    try {
      await TicketApi(ApiClient.instance).create(
        clientLocationId: _selectedLocationId!,
        assetId: _selectedAssetId,
        description: _descriptionController.text.trim(),
        priority: _priority,
      );
      if (mounted) Navigator.of(context).pop(true);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e is ApiException ? e.message : 'No se pudo reportar el ticket.')),
        );
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final showAssetPicker = !_isStaff || _selectedClientIsContract;
    final canSubmit = _selectedLocationId != null &&
        _descriptionController.text.trim().isNotEmpty &&
        (!_isStaff || _selectedClientId != null) &&
        !_submitting;

    return Padding(
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 20,
        bottom: MediaQuery.of(context).viewInsets.bottom + MediaQuery.of(context).padding.bottom + 20,
      ),
      child: _loadingOptions
          ? const SizedBox(height: 160, child: Center(child: CircularProgressIndicator()))
          : _loadError != null
              ? SizedBox(
                  height: 160,
                  child: Center(
                    child: Text(_loadError!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
                  ),
                )
              : SingleChildScrollView(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Reportar ticket', style: Theme.of(context).textTheme.titleLarge),
                      const SizedBox(height: 16),
                      if (_isStaff) ...[
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Expanded(
                              child: DropdownButtonFormField<String>(
                                initialValue: _selectedClientId,
                                isExpanded: true,
                                decoration: const InputDecoration(labelText: 'Cliente *'),
                                items: [
                                  for (final c in _clients)
                                    DropdownMenuItem(value: c.id, child: Text(c.name, overflow: TextOverflow.ellipsis)),
                                ],
                                onChanged: _onClientChanged,
                              ),
                            ),
                            const SizedBox(width: 8),
                            Padding(
                              padding: const EdgeInsets.only(top: 4),
                              child: IconButton.outlined(
                                onPressed: _createClient,
                                icon: const Icon(Icons.add),
                                tooltip: 'Crear cliente',
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                      ],
                      DropdownButtonFormField<String>(
                        initialValue: _selectedLocationId,
                        isExpanded: true,
                        decoration: const InputDecoration(labelText: 'Sede *'),
                        items: [
                          for (final location in _locations)
                            DropdownMenuItem(
                              value: location.id,
                              child: Text('${location.name} — ${location.cityName}', overflow: TextOverflow.ellipsis),
                            ),
                        ],
                        onChanged: (_isStaff && _selectedClientId == null)
                            ? null
                            : (value) => setState(() {
                                  _selectedLocationId = value;
                                  _selectedArea = null;
                                  _selectedAssetId = null;
                                }),
                      ),
                      if (!_isStaff) ...[
                        const SizedBox(height: 12),
                        DropdownButtonFormField<String?>(
                          initialValue: _selectedArea,
                          isExpanded: true,
                          decoration: const InputDecoration(labelText: 'Área (opcional)'),
                          items: [
                            const DropdownMenuItem(value: null, child: Text('Todas las áreas')),
                            for (final area in _areasAtSelectedLocation)
                              DropdownMenuItem(value: area, child: Text(area, overflow: TextOverflow.ellipsis)),
                          ],
                          onChanged: _selectedLocationId == null || _areasAtSelectedLocation.isEmpty
                              ? null
                              : (value) => setState(() {
                                    _selectedArea = value;
                                    _selectedAssetId = null;
                                  }),
                        ),
                      ],
                      if (showAssetPicker) ...[
                        const SizedBox(height: 12),
                        DropdownButtonFormField<String?>(
                          initialValue: _selectedAssetId,
                          isExpanded: true,
                          decoration: const InputDecoration(labelText: 'Equipo (opcional)'),
                          items: [
                            const DropdownMenuItem(value: null, child: Text('Equipo no catalogado / otro')),
                            for (final asset in _assetsAtSelectedLocationAndArea)
                              DropdownMenuItem(
                                value: asset.id,
                                child: Text(
                                  '${asset.assetBrandName} ${asset.model} — ${asset.serialNumber}',
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                          ],
                          onChanged: _selectedLocationId == null ? null : (value) => setState(() => _selectedAssetId = value),
                        ),
                      ],
                      const SizedBox(height: 12),
                      TextField(
                        controller: _descriptionController,
                        minLines: 3,
                        maxLines: 6,
                        onChanged: (_) => setState(() {}),
                        decoration: const InputDecoration(labelText: 'Descripción del problema *'),
                      ),
                      const SizedBox(height: 12),
                      DropdownButtonFormField<String>(
                        initialValue: _priority,
                        decoration: const InputDecoration(labelText: 'Prioridad'),
                        items: [
                          for (final p in _priorities) DropdownMenuItem(value: p, child: Text(p)),
                        ],
                        onChanged: (value) => setState(() => _priority = value ?? 'Media'),
                      ),
                      const SizedBox(height: 20),
                      SizedBox(
                        width: double.infinity,
                        height: 52,
                        child: FilledButton(
                          onPressed: canSubmit ? _submit : null,
                          child: _submitting
                              ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                              : const Text('Reportar', style: TextStyle(fontWeight: FontWeight.w700)),
                        ),
                      ),
                    ],
                  ),
                ),
    );
  }
}

extension _FirstWhereOrNull<T> on List<T> {
  T? firstWhereOrNull(bool Function(T) test) {
    for (final item in this) {
      if (test(item)) return item;
    }
    return null;
  }
}
