import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:latlong2/latlong.dart';

import '../../models/city.dart';
import '../common/location_picker_screen.dart';
import '../../models/client.dart' show supportCoverageLabels;
import '../../models/client_location.dart';
import '../../services/api_client.dart';
import '../../services/device_capture.dart';
import '../../state/client_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';

class ClientDetailScreen extends StatelessWidget {
  const ClientDetailScreen({super.key, required this.clientId});

  final String clientId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => ClientDetailState(ApiClient.instance, clientId)..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('Cliente')),
        body: const _ClientDetailBody(),
      ),
    );
  }
}

class _ClientDetailBody extends StatelessWidget {
  const _ClientDetailBody();

  Future<void> _showEditClientDialog(
    BuildContext context,
    ClientDetailState state,
  ) async {
    final client = state.client!;
    final nameController = TextEditingController(text: client.name);
    final taxIdController = TextEditingController(text: client.taxId ?? '');
    final contactNameController = TextEditingController(
      text: client.contactName ?? '',
    );
    final contactEmailController = TextEditingController(
      text: client.contactEmail ?? '',
    );
    final contactPhoneController = TextEditingController(
      text: client.contactPhone ?? '',
    );
    bool isContractClient = client.isContractClient;
    String supportCoverage = client.supportCoverage;

    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Editar cliente'),
          content: SizedBox(
            width: double.maxFinite,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextField(
                    controller: nameController,
                    decoration: const InputDecoration(labelText: 'Nombre *'),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: taxIdController,
                    decoration: const InputDecoration(labelText: 'NIT'),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: contactNameController,
                    decoration: const InputDecoration(labelText: 'Contacto'),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: contactEmailController,
                    decoration: const InputDecoration(
                      labelText: 'Correo de contacto',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: contactPhoneController,
                    decoration: const InputDecoration(
                      labelText: 'Teléfono de contacto',
                    ),
                  ),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Cliente con contrato'),
                    value: isContractClient,
                    onChanged: (v) =>
                        setDialogState(() => isContractClient = v),
                  ),
                  const SizedBox(height: 8),
                  SegmentedButton<String>(
                    segments: const [
                      ButtonSegment(
                        value: 'HorarioOficina',
                        label: Text('Oficina'),
                      ),
                      ButtonSegment(value: 'Continuo24x7', label: Text('24/7')),
                    ],
                    selected: {supportCoverage},
                    onSelectionChanged: (v) =>
                        setDialogState(() => supportCoverage = v.first),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: nameController.text.trim().isEmpty
                  ? null
                  : () => Navigator.of(dialogContext).pop(true),
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );

    if (result == true && context.mounted) {
      final error = await state.updateClient(
        name: nameController.text.trim(),
        taxId: taxIdController.text.trim().isEmpty
            ? null
            : taxIdController.text.trim(),
        contactName: contactNameController.text.trim().isEmpty
            ? null
            : contactNameController.text.trim(),
        contactEmail: contactEmailController.text.trim().isEmpty
            ? null
            : contactEmailController.text.trim(),
        contactPhone: contactPhoneController.text.trim().isEmpty
            ? null
            : contactPhoneController.text.trim(),
        isContractClient: isContractClient,
        supportCoverage: supportCoverage,
      );
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(error ?? 'Cliente actualizado.')),
        );
      }
    }
  }

  Future<void> _showLocationDialog(
    BuildContext context,
    ClientDetailState state, {
    ClientLocation? existing,
  }) async {
    String? cityId = existing?.cityId;
    final nameController = TextEditingController(text: existing?.name ?? '');
    final addressController = TextEditingController(
      text: existing?.address ?? '',
    );
    final contactNameController = TextEditingController(
      text: existing?.contactName ?? '',
    );
    final contactPhoneController = TextEditingController(
      text: existing?.contactPhone ?? '',
    );
    final latitudeController = TextEditingController(
      text: existing?.latitude?.toString() ?? '',
    );
    final longitudeController = TextEditingController(
      text: existing?.longitude?.toString() ?? '',
    );

    double? parseCoordinate(TextEditingController c) =>
        double.tryParse(c.text.trim().replaceAll(',', '.'));
    // Van juntas: ambas o ninguna, y dentro de rango.
    bool coordinatesValid() {
      final lat = parseCoordinate(latitudeController);
      final lon = parseCoordinate(longitudeController);
      if (lat == null && lon == null) {
        return latitudeController.text.trim().isEmpty &&
            longitudeController.text.trim().isEmpty;
      }
      return lat != null &&
          lon != null &&
          lat >= -90 &&
          lat <= 90 &&
          lon >= -180 &&
          lon <= 180;
    }

    final result = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: Text(existing == null ? 'Agregar sede' : 'Editar sede'),
          content: SizedBox(
            width: double.maxFinite,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  DropdownButtonFormField<String>(
                    initialValue: cityId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Ciudad *'),
                    items: [
                      for (final City city in state.cities)
                        DropdownMenuItem(
                          value: city.id,
                          child: Text(
                            '${city.name} — ${city.stateOrProvince}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                    ],
                    onChanged: (value) => setDialogState(() => cityId = value),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: nameController,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Nombre de la sede *',
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: addressController,
                    onChanged: (_) => setDialogState(() {}),
                    decoration: const InputDecoration(labelText: 'Dirección *'),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: contactNameController,
                    decoration: const InputDecoration(labelText: 'Contacto'),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: contactPhoneController,
                    decoration: const InputDecoration(labelText: 'Teléfono'),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: TextField(
                          controller: latitudeController,
                          onChanged: (_) => setDialogState(() {}),
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                            signed: true,
                          ),
                          decoration: const InputDecoration(labelText: 'Latitud'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: TextField(
                          controller: longitudeController,
                          onChanged: (_) => setDialogState(() {}),
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                            signed: true,
                          ),
                          decoration: const InputDecoration(labelText: 'Longitud'),
                        ),
                      ),
                    ],
                  ),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: () async {
                        final lat = parseCoordinate(latitudeController);
                        final lon = parseCoordinate(longitudeController);
                        final picked = await Navigator.of(dialogContext).push<LatLng>(
                          MaterialPageRoute(
                            builder: (_) => LocationPickerScreen(
                              initial: lat != null && lon != null ? LatLng(lat, lon) : null,
                              searchHint: addressController.text.trim(),
                            ),
                          ),
                        );
                        if (picked == null) return;
                        latitudeController.text = picked.latitude.toString();
                        longitudeController.text = picked.longitude.toString();
                        setDialogState(() {});
                      },
                      icon: const Icon(Icons.map_outlined, size: 18),
                      label: const Text('Elegir en el mapa'),
                    ),
                  ),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: () async {
                        final fix = await DeviceCapture.currentPosition();
                        if (fix == null) {
                          if (dialogContext.mounted) {
                            ScaffoldMessenger.of(dialogContext).showSnackBar(
                              const SnackBar(
                                content: Text('No se pudo obtener la ubicación. Revisa el permiso.'),
                              ),
                            );
                          }
                          return;
                        }
                        latitudeController.text = fix.latitude.toStringAsFixed(6);
                        longitudeController.text = fix.longitude.toStringAsFixed(6);
                        setDialogState(() {});
                      },
                      icon: const Icon(Icons.my_location, size: 18),
                      label: const Text('Usar mi ubicación'),
                    ),
                  ),
                  const Text(
                    'Opcional. Con las coordenadas se verifica que el técnico llegó a la sede.',
                    style: TextStyle(fontSize: 12),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed:
                  cityId == null ||
                      nameController.text.trim().isEmpty ||
                      addressController.text.trim().isEmpty ||
                      !coordinatesValid()
                  ? null
                  : () => Navigator.of(dialogContext).pop(true),
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );

    if (result == true && context.mounted) {
      final contactName = contactNameController.text.trim().isEmpty
          ? null
          : contactNameController.text.trim();
      final contactPhone = contactPhoneController.text.trim().isEmpty
          ? null
          : contactPhoneController.text.trim();
      final error = existing == null
          ? await state.addLocation(
              cityId: cityId!,
              name: nameController.text.trim(),
              address: addressController.text.trim(),
              contactName: contactName,
              contactPhone: contactPhone,
              latitude: parseCoordinate(latitudeController),
              longitude: parseCoordinate(longitudeController),
            )
          : await state.updateLocation(
              existing.id,
              cityId: cityId!,
              name: nameController.text.trim(),
              address: addressController.text.trim(),
              contactName: contactName,
              contactPhone: contactPhone,
              latitude: parseCoordinate(latitudeController),
              longitude: parseCoordinate(longitudeController),
            );
      if (context.mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(error ?? 'Sede guardada.')));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<ClientDetailState>(
      builder: (context, state, _) {
        if (state.loading && state.client == null) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.client == null) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(
                state.error!,
                style: const TextStyle(color: AppColors.signalRed),
                textAlign: TextAlign.center,
              ),
            ),
          );
        }
        final client = state.client!;
        return RefreshIndicator(
          onRefresh: state.load,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              ClaySurface(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            client.name,
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                        ),
                        StatusChip.activeState(client.isActive),
                      ],
                    ),
                    if (client.taxId != null)
                      Text(
                        'NIT: ${client.taxId}',
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    if (client.contactName != null)
                      Text(
                        client.contactName!,
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    if (client.contactEmail != null)
                      Text(
                        client.contactEmail!,
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    if (client.contactPhone != null)
                      Text(
                        client.contactPhone!,
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    const SizedBox(height: 4),
                    Text(
                      '${client.isContractClient ? 'Cliente con contrato' : 'Cliente externo'} · Soporte ${supportCoverageLabels[client.supportCoverage] ?? client.supportCoverage}',
                      style: const TextStyle(
                        fontSize: 12,
                        fontStyle: FontStyle.italic,
                      ),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: state.busyWithAction
                                ? null
                                : () => _showEditClientDialog(context, state),
                            icon: const Icon(Icons.edit_outlined, size: 18),
                            label: const Text('Editar'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: FilledButton.icon(
                            onPressed: state.busyWithAction
                                ? null
                                : () => state.setClientStatus(!client.isActive),
                            style: client.isActive
                                ? FilledButton.styleFrom(
                                    backgroundColor: AppColors.signalRed,
                                  )
                                : null,
                            icon: Icon(
                              client.isActive
                                  ? Icons.block
                                  : Icons.check_circle_outline,
                              size: 18,
                            ),
                            label: Text(
                              client.isActive ? 'Desactivar' : 'Activar',
                            ),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              Row(
                children: [
                  Expanded(
                    child: Text(
                      'Sedes',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  TextButton.icon(
                    onPressed: state.busyWithAction
                        ? null
                        : () => _showLocationDialog(context, state),
                    icon: const Icon(Icons.add, size: 18),
                    label: const Text('Agregar'),
                  ),
                ],
              ),
              if (state.locations.isEmpty)
                const ClaySurface(
                  child: Text(
                    'Sin sedes registradas.',
                    style: TextStyle(color: AppColors.inkSecondary),
                  ),
                )
              else
                for (final location in state.locations)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: ClaySurface(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: Text(
                                  location.name,
                                  style: const TextStyle(
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              StatusChip.activeState(location.isActive),
                            ],
                          ),
                          Text(
                            location.cityName,
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                            ),
                          ),
                          Text(
                            location.address,
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                              fontSize: 12,
                            ),
                          ),
                          if (location.contactName != null)
                            Text(
                              '${location.contactName}${location.contactPhone != null ? ' — ${location.contactPhone}' : ''}',
                              style: const TextStyle(
                                color: AppColors.inkSecondary,
                                fontSize: 12,
                              ),
                            ),
                          const SizedBox(height: 8),
                          Row(
                            children: [
                              Expanded(
                                child: OutlinedButton.icon(
                                  onPressed: state.busyWithAction
                                      ? null
                                      : () => _showLocationDialog(
                                          context,
                                          state,
                                          existing: location,
                                        ),
                                  icon: const Icon(
                                    Icons.edit_outlined,
                                    size: 16,
                                  ),
                                  label: const Text('Editar'),
                                ),
                              ),
                              const SizedBox(width: 8),
                              Expanded(
                                child: OutlinedButton.icon(
                                  onPressed: state.busyWithAction
                                      ? null
                                      : () => state.setLocationStatus(
                                          location.id,
                                          !location.isActive,
                                        ),
                                  style: OutlinedButton.styleFrom(
                                    foregroundColor: location.isActive
                                        ? AppColors.signalRed
                                        : AppColors.signalBlue,
                                  ),
                                  icon: Icon(
                                    location.isActive
                                        ? Icons.block
                                        : Icons.check_circle_outline,
                                    size: 16,
                                  ),
                                  label: Text(
                                    location.isActive
                                        ? 'Desactivar'
                                        : 'Activar',
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
            ],
          ),
        );
      },
    );
  }
}
