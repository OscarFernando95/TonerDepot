import 'package:flutter/material.dart';

import '../models/client_location.dart';
import '../models/client_location_access.dart';
import '../services/api_client.dart';
import '../services/client_location_api.dart';
import '../theme/app_theme.dart';
import 'clay_segmented_control.dart';

/// Control "Acceso a sedes" de un usuario Cliente: "Todas las sedes de la
/// empresa" (por defecto) o "Solo algunas sedes" con un checklist de las sedes
/// de `clientId`. Es controlado (`value`/`onChanged`); la lógica vive en
/// [ClientLocationAccess]. Carga las sedes por su cuenta y las vuelve a pedir
/// si cambia `clientId` — quien lo usa es quien reinicia `value` en ese caso.
class ClientLocationAccessPicker extends StatefulWidget {
  const ClientLocationAccessPicker({
    super.key,
    required this.clientId,
    required this.value,
    required this.onChanged,
  });

  final String clientId;
  final ClientLocationAccess value;
  final ValueChanged<ClientLocationAccess> onChanged;

  @override
  State<ClientLocationAccessPicker> createState() =>
      _ClientLocationAccessPickerState();
}

class _ClientLocationAccessPickerState
    extends State<ClientLocationAccessPicker> {
  List<ClientLocation> _locations = [];
  bool _loading = true;
  String? _loadError;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(ClientLocationAccessPicker oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.clientId != widget.clientId) _load();
  }

  Future<void> _load() async {
    final requestedClientId = widget.clientId;
    setState(() {
      _loading = true;
      _loadError = null;
      _locations = [];
    });
    try {
      final locations = await ClientLocationApi(ApiClient.instance)
          .listForClient(requestedClientId);
      // Si mientras tanto eligieron otra empresa, esta respuesta ya no aplica.
      if (!mounted || requestedClientId != widget.clientId) return;
      setState(() {
        _locations = locations;
        _loading = false;
      });
    } catch (e, st) {
      debugPrint('ClientLocationAccessPicker._load failed: $e\n$st');
      if (!mounted || requestedClientId != widget.clientId) return;
      setState(() {
        _loading = false;
        _loadError = e is ApiException
            ? e.message
            : 'No se pudieron cargar las sedes.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final value = widget.value;
    final selectable = ClientLocationAccess.selectable(
      _locations,
      value.selectedIds,
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Acceso a sedes',
          style: TextStyle(
            fontWeight: FontWeight.w700,
            color: AppColors.inkPrimary,
          ),
        ),
        const SizedBox(height: 8),
        ClaySegmentedControl<bool>(
          segments: const [
            ClaySegment(value: false, label: 'Todas las sedes'),
            ClaySegment(value: true, label: 'Solo algunas'),
          ],
          selected: value.restricted,
          onChanged: (restricted) =>
              widget.onChanged(value.withRestricted(restricted)),
        ),
        const SizedBox(height: 6),
        if (!value.restricted)
          const Text(
            'Podrá ver y reportar tickets de todas las sedes de su empresa, incluidas las que se agreguen después.',
            style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          )
        else if (_loading)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 16),
            child: Center(child: CircularProgressIndicator()),
          )
        else if (_loadError != null)
          Row(
            children: [
              Expanded(
                child: Text(
                  _loadError!,
                  style: const TextStyle(
                    color: AppColors.signalRed,
                    fontSize: 12,
                  ),
                ),
              ),
              TextButton(onPressed: _load, child: const Text('Reintentar')),
            ],
          )
        else if (selectable.isEmpty)
          const Text(
            'Esta empresa no tiene sedes activas.',
            style: TextStyle(color: AppColors.signalRed, fontSize: 12),
          )
        else ...[
          for (final location in selectable)
            CheckboxListTile(
              dense: true,
              contentPadding: EdgeInsets.zero,
              controlAffinity: ListTileControlAffinity.leading,
              value: value.selectedIds.contains(location.id),
              onChanged: (checked) =>
                  widget.onChanged(value.toggle(location.id, checked ?? false)),
              title: Text(location.name),
              subtitle: Text(
                location.isActive
                    ? location.cityName
                    : '${location.cityName} · inactiva',
                style: const TextStyle(
                  color: AppColors.inkSecondary,
                  fontSize: 12,
                ),
              ),
            ),
          if (value.error != null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                value.error!,
                style: const TextStyle(
                  color: AppColors.signalRed,
                  fontSize: 12,
                ),
              ),
            ),
        ],
      ],
    );
  }
}
