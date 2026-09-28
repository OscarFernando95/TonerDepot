import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/asset_brand.dart';
import '../../models/asset_model.dart';
import '../../models/client.dart';
import '../../models/client_location.dart';
import '../../models/status_labels.dart';
import '../../services/api_client.dart';
import '../../services/asset_brand_api.dart';
import '../../services/asset_model_api.dart';
import '../../services/client_api.dart';
import '../../services/client_location_api.dart';
import '../../state/asset_detail_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';

class AssetDetailScreen extends StatelessWidget {
  const AssetDetailScreen({super.key, required this.assetId});

  final String assetId;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => AssetDetailState(ApiClient.instance, assetId)..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('Activo')),
        body: const _AssetDetailBody(),
      ),
    );
  }
}

class _AssetDetailBody extends StatelessWidget {
  const _AssetDetailBody();

  String _formatDate(String iso) => iso.split('T').first;

  Future<void> _showEditDialog(
    BuildContext context,
    AssetDetailState state,
  ) async {
    final asset = state.asset!;
    List<AssetBrand> brands = [];
    List<AssetModel> models = [];
    String? brandId;
    String? modelId = asset.assetModelId;
    String type = asset.type;
    final serialController = TextEditingController(text: asset.serialNumber);
    bool loading = true;

    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) {
          if (loading) {
            AssetBrandApi(ApiClient.instance).list().then((loaded) async {
              brands = loaded;
              // Preseleccionar la marca actual buscando por nombre — Asset
              // no trae assetBrandId, solo el nombre ya resuelto.
              final current = loaded.where(
                (b) => b.name == asset.assetBrandName,
              );
              if (current.isNotEmpty) {
                brandId = current.first.id;
                models = await AssetModelApi(ApiClient.instance)
                    .listForBrand(brandId!);
              }
              setDialogState(() => loading = false);
            });
            return const AlertDialog(
              content: SizedBox(
                height: 120,
                child: Center(child: CircularProgressIndicator()),
              ),
            );
          }
          return AlertDialog(
            title: const Text('Editar activo'),
            content: SizedBox(
              width: double.maxFinite,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    DropdownButtonFormField<String>(
                      initialValue: brandId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Marca *'),
                      items: [
                        for (final b in brands)
                          DropdownMenuItem(value: b.id, child: Text(b.name)),
                      ],
                      onChanged: (value) async {
                        final newModels = value == null
                            ? <AssetModel>[]
                            : await AssetModelApi(ApiClient.instance)
                                  .listForBrand(value);
                        setDialogState(() {
                          brandId = value;
                          modelId = null;
                          models = newModels;
                        });
                      },
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: modelId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Modelo *'),
                      items: [
                        for (final m in models)
                          DropdownMenuItem(value: m.id, child: Text(m.name)),
                      ],
                      onChanged: brandId == null
                          ? null
                          : (value) => setDialogState(() => modelId = value),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: serialController,
                      onChanged: (_) => setDialogState(() {}),
                      decoration: const InputDecoration(
                        labelText: 'Número de serie *',
                      ),
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: type,
                      decoration: const InputDecoration(labelText: 'Tipo'),
                      items: const [
                        DropdownMenuItem(
                          value: 'Impresora',
                          child: Text('Impresora'),
                        ),
                        DropdownMenuItem(
                          value: 'ComputoEquipo',
                          child: Text('ComputoEquipo'),
                        ),
                      ],
                      onChanged: (value) =>
                          setDialogState(() => type = value ?? 'Impresora'),
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(dialogContext).pop(),
                child: const Text('Cancelar'),
              ),
              FilledButton(
                onPressed:
                    modelId == null || serialController.text.trim().isEmpty
                    ? null
                    : () async {
                        Navigator.of(dialogContext).pop();
                        final error = await state.updateAsset(
                          assetModelId: modelId!,
                          serialNumber: serialController.text.trim(),
                          type: type,
                        );
                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(
                              content: Text(error ?? 'Activo actualizado.'),
                            ),
                          );
                        }
                      },
                child: const Text('Guardar'),
              ),
            ],
          );
        },
      ),
    );
  }

  Future<void> _showChangeStatusDialog(
    BuildContext context,
    AssetDetailState state,
  ) async {
    final asset = state.asset!;
    final allowed = Asset.allowedTransitions[asset.lifecycleStatus] ?? [];
    if (allowed.isEmpty) return;

    String newStatus = allowed.first;
    List<Client> clients = [];
    List<ClientLocation> locations = [];
    String? clientId;
    String? clientLocationId;
    final areaController = TextEditingController();
    final notesController = TextEditingController();
    bool needsLocation =
        newStatus == 'Instalado' || newStatus == 'PendienteInstalacion';
    bool loadingClients = needsLocation;

    if (needsLocation) {
      clients = await ClientApi(ApiClient.instance).list();
      if (!context.mounted) return;
    }

    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialogState) => AlertDialog(
          title: const Text('Cambiar estado'),
          content: SizedBox(
            width: double.maxFinite,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DropdownButtonFormField<String>(
                    initialValue: newStatus,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Nuevo estado',
                    ),
                    items: [
                      for (final s in allowed)
                        DropdownMenuItem(
                          value: s,
                          child: Text((StatusLabels.assetLifecycle[s] ?? s)),
                        ),
                    ],
                    onChanged: (value) async {
                      final requiresLocation =
                          value == 'Instalado' ||
                          value == 'PendienteInstalacion';
                      if (requiresLocation && clients.isEmpty) {
                        setDialogState(() => loadingClients = true);
                        clients = await ClientApi(ApiClient.instance).list();
                      }
                      setDialogState(() {
                        newStatus = value ?? newStatus;
                        needsLocation = requiresLocation;
                        loadingClients = false;
                        clientId = null;
                        clientLocationId = null;
                        locations = [];
                      });
                    },
                  ),
                  if (needsLocation) ...[
                    const SizedBox(height: 12),
                    if (loadingClients)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 12),
                        child: LinearProgressIndicator(),
                      )
                    else
                      DropdownButtonFormField<String>(
                        initialValue: clientId,
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'Cliente *',
                        ),
                        items: [
                          for (final c in clients)
                            DropdownMenuItem(value: c.id, child: Text(c.name)),
                        ],
                        onChanged: (value) async {
                          final newLocations = value == null
                              ? <ClientLocation>[]
                              : await ClientLocationApi(ApiClient.instance)
                                    .listForClient(value);
                          setDialogState(() {
                            clientId = value;
                            clientLocationId = null;
                            locations = newLocations;
                          });
                        },
                      ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      initialValue: clientLocationId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Sede *'),
                      items: [
                        for (final l in locations)
                          DropdownMenuItem(
                            value: l.id,
                            child: Text(
                              '${l.name} — ${l.cityName}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                      ],
                      onChanged: clientId == null
                          ? null
                          : (value) =>
                                setDialogState(() => clientLocationId = value),
                    ),
                    if (newStatus == 'Instalado') ...[
                      const SizedBox(height: 12),
                      TextField(
                        controller: areaController,
                        onChanged: (_) => setDialogState(() {}),
                        decoration: const InputDecoration(labelText: 'Área *'),
                      ),
                    ],
                  ],
                  const SizedBox(height: 12),
                  TextField(
                    controller: notesController,
                    decoration: const InputDecoration(
                      labelText: 'Notas (opcional)',
                    ),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed:
                  needsLocation &&
                      (clientLocationId == null ||
                          (newStatus == 'Instalado' &&
                              areaController.text.trim().isEmpty))
                  ? null
                  : () async {
                      Navigator.of(dialogContext).pop();
                      final error = await state.changeStatus(
                        newStatus: newStatus,
                        clientLocationId: clientLocationId,
                        area: newStatus == 'Instalado'
                            ? areaController.text.trim()
                            : null,
                        notes: notesController.text.trim().isEmpty
                            ? null
                            : notesController.text.trim(),
                      );
                      if (context.mounted) {
                        ScaffoldMessenger.of(context).showSnackBar(
                          SnackBar(
                            content: Text(error ?? 'Estado actualizado.'),
                          ),
                        );
                      }
                    },
              child: const Text('Guardar'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<AssetDetailState>(
      builder: (context, state, _) {
        if (state.loading && state.asset == null) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.asset == null) {
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
        final asset = state.asset!;
        final canChangeStatus =
            (Asset.allowedTransitions[asset.lifecycleStatus] ?? []).isNotEmpty;
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
                            '${asset.assetBrandName} ${asset.model}',
                            style: Theme.of(context).textTheme.titleMedium,
                          ),
                        ),
                        StatusChip.assetLifecycle(asset.lifecycleStatus),
                      ],
                    ),
                    Text(
                      'Serie: ${asset.serialNumber}',
                      style: const TextStyle(color: AppColors.inkSecondary),
                    ),
                    Text(
                      'Tipo: ${asset.type}',
                      style: const TextStyle(color: AppColors.inkSecondary),
                    ),
                    if (asset.currentClientName != null)
                      Text(
                        '${asset.currentClientName} — ${asset.currentClientLocationName}${asset.cityName != null ? ' (${asset.cityName})' : ''}',
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    if (asset.area != null)
                      Text(
                        'Área: ${asset.area}',
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    if (asset.lastMeterReading != null)
                      Text(
                        'Último contador: ${asset.lastMeterReading}',
                        style: const TextStyle(color: AppColors.inkSecondary)
                            .merge(AppTextStyles.tabularNumber),
                      ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: state.busyWithAction
                                ? null
                                : () => _showEditDialog(context, state),
                            icon: const Icon(Icons.edit_outlined, size: 18),
                            label: const Text('Editar'),
                          ),
                        ),
                        if (canChangeStatus) ...[
                          const SizedBox(width: 8),
                          Expanded(
                            child: FilledButton.icon(
                              onPressed: state.busyWithAction
                                  ? null
                                  : () =>
                                        _showChangeStatusDialog(context, state),
                              icon: const Icon(Icons.sync_alt, size: 18),
                              label: const Text('Cambiar estado'),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              Text(
                'Historial de estados',
                style: Theme.of(context).textTheme.titleSmall,
              ),
              const SizedBox(height: 8),
              if (state.statusHistory.isEmpty)
                const ClaySurface(
                  child: Text(
                    'Sin cambios de estado registrados.',
                    style: TextStyle(color: AppColors.inkSecondary),
                  ),
                )
              else
                for (final log in state.statusHistory)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: ClaySurface(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 16,
                        vertical: 12,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            '${(StatusLabels.assetLifecycle[log.previousStatus] ?? log.previousStatus)} → ${(StatusLabels.assetLifecycle[log.newStatus] ?? log.newStatus)}',
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          Text(
                            '${_formatDate(log.changedAt)}${log.changedByUserName != null ? ' — ${log.changedByUserName}' : ''}',
                            style: const TextStyle(
                              color: AppColors.inkSecondary,
                              fontSize: 12,
                            ),
                          ),
                          if (log.notes != null)
                            Text(
                              log.notes!,
                              style: const TextStyle(
                                fontSize: 12,
                                fontStyle: FontStyle.italic,
                              ),
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
