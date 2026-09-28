import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/client_location.dart';
import '../../services/api_client.dart';
import '../../services/asset_api.dart';
import '../../services/client_location_api.dart';
import '../../services/ticket_api.dart';
import '../../state/auth_state.dart';
import '../../theme/app_theme.dart';

/// Formulario de creación de ticket para Cliente. Opaco/clay a propósito, no
/// glass — es un formulario de datos reales (igual criterio que
/// checkout_sheet.dart), no chrome decorativo.
class TicketCreateSheet extends StatefulWidget {
  const TicketCreateSheet({super.key});

  static Future<bool?> show(BuildContext context) {
    return showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.only(
          topLeft: Radius.circular(24),
          topRight: Radius.circular(24),
        ),
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
  List<ClientLocation> _locations = [];
  List<Asset> _assets = [];
  String? _selectedLocationId;
  String? _selectedAssetId;
  String _priority = 'Media';

  List<Asset> get _assetsAtSelectedLocation => _assets
      .where((a) => a.currentClientLocationId == _selectedLocationId)
      .toList();

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
    final clientId = context.read<AuthState>().currentUser?.clientId;
    if (clientId == null) {
      setState(() {
        _loadingOptions = false;
        _loadError = 'Tu usuario no tiene un cliente asociado.';
      });
      return;
    }
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
        _loadError = e is ApiException
            ? e.message
            : 'No se pudieron cargar tus sedes.';
      });
    }
  }

  Future<void> _submit() async {
    if (_selectedLocationId == null ||
        _descriptionController.text.trim().isEmpty) {
      return;
    }
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
          SnackBar(
            content: Text(
              e is ApiException ? e.message : 'No se pudo reportar el ticket.',
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
    return Padding(
      padding: EdgeInsets.only(
        left: 20,
        right: 20,
        top: 20,
        bottom:
            MediaQuery.of(context).viewInsets.bottom +
            MediaQuery.of(context).padding.bottom +
            20,
      ),
      child: _loadingOptions
          ? const SizedBox(
              height: 160,
              child: Center(child: CircularProgressIndicator()),
            )
          : _loadError != null
          ? SizedBox(
              height: 160,
              child: Center(
                child: Text(
                  _loadError!,
                  style: const TextStyle(color: AppColors.signalRed),
                  textAlign: TextAlign.center,
                ),
              ),
            )
          : SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Reportar ticket',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 16),
                  DropdownButtonFormField<String>(
                    initialValue: _selectedLocationId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Sede *'),
                    items: [
                      for (final location in _locations)
                        DropdownMenuItem(
                          value: location.id,
                          child: Text(
                            '${location.name} — ${location.cityName}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                    ],
                    onChanged: (value) => setState(() {
                      _selectedLocationId = value;
                      _selectedAssetId = null;
                    }),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String?>(
                    initialValue: _selectedAssetId,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Equipo (opcional)',
                    ),
                    items: [
                      const DropdownMenuItem(
                        value: null,
                        child: Text('Equipo no catalogado / otro'),
                      ),
                      for (final asset in _assetsAtSelectedLocation)
                        DropdownMenuItem(
                          value: asset.id,
                          child: Text(
                            '${asset.assetBrandName} ${asset.model} — ${asset.serialNumber}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                    ],
                    onChanged: _selectedLocationId == null
                        ? null
                        : (value) => setState(() => _selectedAssetId = value),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _descriptionController,
                    minLines: 3,
                    maxLines: 6,
                    onChanged: (_) => setState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Descripción del problema *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: _priority,
                    decoration: const InputDecoration(labelText: 'Prioridad'),
                    items: [
                      for (final p in _priorities)
                        DropdownMenuItem(value: p, child: Text(p)),
                    ],
                    onChanged: (value) =>
                        setState(() => _priority = value ?? 'Media'),
                  ),
                  const SizedBox(height: 20),
                  SizedBox(
                    width: double.infinity,
                    height: 52,
                    child: FilledButton(
                      onPressed:
                          _selectedLocationId != null &&
                              _descriptionController.text.trim().isNotEmpty &&
                              !_submitting
                          ? _submit
                          : null,
                      child: _submitting
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text(
                              'Reportar',
                              style: TextStyle(fontWeight: FontWeight.w700),
                            ),
                    ),
                  ),
                ],
              ),
            ),
    );
  }
}
