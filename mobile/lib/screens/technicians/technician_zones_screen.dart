import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/zone.dart';
import '../../services/api_client.dart';
import '../../state/technician_zones_state.dart';
import '../../state/technician_linked_assets_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';

/// Detalle de gestión de un técnico — zonas (de las que se deriva su cobertura de municipios) y activos
/// vinculados (ver TechnicianLinkedAssetsState: gobierna qué ve el técnico
/// en "Lectura de contadores"). `extra` del push (ver router/app_router.dart)
/// trae el nombre para no tener que volver a listar técnicos solo para el
/// título del AppBar.
class TechnicianZonesScreen extends StatelessWidget {
  const TechnicianZonesScreen({
    super.key,
    required this.technicianId,
    this.technicianName,
  });

  final String technicianId;
  final String? technicianName;

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => TechnicianZonesState(ApiClient.instance, technicianId)..load()),
        ChangeNotifierProvider(create: (_) => TechnicianLinkedAssetsState(ApiClient.instance, technicianId)..load()),
      ],
      child: DefaultTabController(
        length: 2,
        child: Scaffold(
          appBar: AppBar(
            title: Text(technicianName ?? 'Técnico'),
            bottom: const TabBar(
              tabs: [
                Tab(text: 'Zonas'),
                Tab(text: 'Activos vinculados'),
              ],
            ),
          ),
          body: const TabBarView(
            children: [_ZonesBody(), _LinkedAssetsBody()],
          ),
        ),
      ),
    );
  }
}

class _ZonesBody extends StatelessWidget {
  const _ZonesBody();

  void _snack(BuildContext context, String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  /// Elige una zona (selección única) entre las `candidates`. null = cancelado.
  Future<String?> _pickZone(BuildContext context, List<Zone> candidates, String title) {
    return showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.only(topLeft: Radius.circular(24), topRight: Radius.circular(24)),
      ),
      builder: (sheetContext) => DraggableScrollableSheet(
        initialChildSize: 0.6,
        minChildSize: 0.4,
        maxChildSize: 0.9,
        expand: false,
        builder: (sheetContext, scrollController) => Padding(
          padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.of(sheetContext).padding.bottom + 16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(title, style: Theme.of(sheetContext).textTheme.titleMedium),
              const SizedBox(height: 12),
              Expanded(
                child: candidates.isEmpty
                    ? const Center(
                        child: Text(
                          'No hay más zonas disponibles.',
                          style: TextStyle(color: AppColors.inkSecondary),
                        ),
                      )
                    : ListView.builder(
                        controller: scrollController,
                        itemCount: candidates.length,
                        itemBuilder: (context, index) {
                          final zone = candidates[index];
                          return ClayCard(
                            onTap: () => Navigator.of(sheetContext).pop(zone.id),
                            child: Row(
                              children: [
                                const ClayIconBadge(icon: Icons.map_outlined, color: AppColors.signalBlue),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(zone.name, style: const TextStyle(fontWeight: FontWeight.w600)),
                                      Text(
                                        zone.cities.isEmpty
                                            ? 'Sin municipios'
                                            : zone.cities.map((c) => c.name).join(', '),
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _assign(BuildContext context, TechnicianZonesState state, {String? replacing}) async {
    final zoneId = await _pickZone(
      context,
      state.availableZones,
      replacing == null ? 'Elegir zona' : 'Cambiar de zona',
    );
    if (zoneId == null || !context.mounted) return;
    final error = replacing == null ? await state.addZone(zoneId) : await state.replaceZone(replacing, zoneId);
    if (error != null && context.mounted) _snack(context, error);
  }

  Future<void> _remove(BuildContext context, TechnicianZonesState state, TechnicianZone zone) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Quitar zona'),
        content: Text('¿Quitar la zona "${zone.zoneName}" de este técnico? Dejará de cubrir sus municipios.'),
        actions: [
          TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Quitar')),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    final error = await state.removeZone(zone.zoneId);
    if (error != null && context.mounted) _snack(context, error);
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<TechnicianZonesState>(
      builder: (context, state, _) {
        if (state.loading && state.allZones.isEmpty && state.zones.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.allZones.isEmpty && state.zones.isEmpty) {
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
        final canAssign = !state.busyWithAction && state.availableZones.isNotEmpty;
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (state.allZones.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(bottom: 12),
                  child: Text(
                    'Todavía no hay zonas creadas. Créalas en "Zonas" del menú.',
                    style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                  ),
                ),
              FilledButton.icon(
                onPressed: canAssign
                    ? () => _assign(
                        context,
                        state,
                        replacing: state.zones.length == 1 ? state.zones.first.zoneId : null,
                      )
                    : null,
                icon: Icon(state.zones.length == 1 ? Icons.swap_horiz : Icons.map_outlined, size: 18),
                label: Text(state.zones.length == 1 ? 'Cambiar zona' : 'Asignar zona'),
              ),
              if (state.zones.isNotEmpty) ...[
                const SizedBox(height: 4),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton.icon(
                    onPressed: canAssign ? () => _assign(context, state) : null,
                    icon: const Icon(Icons.add, size: 18),
                    label: const Text('Agregar otra zona'),
                  ),
                ),
              ],
              const SizedBox(height: 8),
              Expanded(
                child: state.zones.isEmpty
                    ? const Center(
                        child: Text(
                          'Sin zona asignada todavía.\nNo cubre ningún municipio.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: AppColors.inkSecondary),
                        ),
                      )
                    : ListView.builder(
                        itemCount: state.zones.length,
                        itemBuilder: (context, index) {
                          final item = state.zones[index];
                          return ClayCard(
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const ClayIconBadge(icon: Icons.map_outlined, color: AppColors.signalBlue),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(item.zoneName, style: const TextStyle(fontWeight: FontWeight.w600)),
                                      const SizedBox(height: 2),
                                      Text(
                                        item.cityNames.isEmpty
                                            ? 'Sin municipios en esta zona'
                                            : item.cityNames.join(', '),
                                        style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                                      ),
                                    ],
                                  ),
                                ),
                                IconButton(
                                  icon: const Icon(Icons.delete_outline, color: AppColors.signalRed),
                                  tooltip: 'Quitar zona',
                                  onPressed: state.busyWithAction ? null : () => _remove(context, state, item),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
              ),
            ],
          ),
        );
      },
    );
  }
}

class _LinkedAssetsBody extends StatelessWidget {
  const _LinkedAssetsBody();

  Future<void> _showLinkAssetSheet(BuildContext context, TechnicianLinkedAssetsState state) async {
    final searchController = TextEditingController();
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.claySurface,
      shape: const RoundedRectangleBorder(),
      builder: (sheetContext) {
        return DraggableScrollableSheet(
          initialChildSize: 0.85,
          minChildSize: 0.5,
          maxChildSize: 0.95,
          expand: false,
          builder: (sheetContext, scrollController) {
            return ChangeNotifierProvider.value(
              value: state,
              child: Padding(
                padding: EdgeInsets.fromLTRB(16, 16, 16, MediaQuery.of(sheetContext).viewInsets.bottom + MediaQuery.of(sheetContext).padding.bottom + 16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('Vincular activo', style: Theme.of(sheetContext).textTheme.titleMedium),
                    const SizedBox(height: 12),
                    TextField(
                      controller: searchController,
                      decoration: const InputDecoration(
                        labelText: 'Buscar por ciudad, cliente, sede, serie o modelo',
                        prefixIcon: Icon(Icons.search),
                      ),
                      onChanged: state.setSearchQuery,
                    ),
                    const SizedBox(height: 12),
                    Consumer<TechnicianLinkedAssetsState>(
                      builder: (context, state, _) {
                        final available = state.availableAssets;
                        return Row(
                          children: [
                            Expanded(
                              child: Text(
                                '${available.length} activo(s) disponibles',
                                style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                              ),
                            ),
                            TextButton.icon(
                              onPressed: state.busyWithAction || available.isEmpty
                                  ? null
                                  : () async {
                                      final error = await state.linkAllVisible();
                                      if (error != null && sheetContext.mounted) {
                                        ScaffoldMessenger.of(sheetContext).showSnackBar(SnackBar(content: Text(error)));
                                      }
                                    },
                              icon: const Icon(Icons.playlist_add_check, size: 18),
                              label: const Text('Vincular todos'),
                            ),
                          ],
                        );
                      },
                    ),
                    Expanded(
                      child: Consumer<TechnicianLinkedAssetsState>(
                        builder: (context, state, _) {
                          final available = state.availableAssets;
                          if (available.isEmpty) {
                            return const Center(
                              child: Text('No hay activos instalados disponibles para vincular.',
                                  style: TextStyle(color: AppColors.inkSecondary)),
                            );
                          }
                          return ListView.builder(
                            controller: scrollController,
                            itemCount: available.length,
                            itemBuilder: (context, index) {
                              final Asset asset = available[index];
                              return ClayCard(
                                padding: EdgeInsets.zero,
                                child: ListTile(
                                  leading: const ClayIconBadge(icon: Icons.print_outlined, color: AppColors.signalBlue),
                                  title: Text('${asset.assetBrandName} ${asset.model}'),
                                  subtitle: Text(
                                    '${asset.currentClientName ?? 'Sin cliente'}'
                                    '${asset.area != null && asset.area!.isNotEmpty ? ' · ${asset.area}' : ''}\n'
                                    'Serie: ${asset.serialNumber}'
                                    '${asset.cityName != null ? ' · ${asset.cityName}' : ''}',
                                    style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                                  ),
                                  isThreeLine: true,
                                  contentPadding: const EdgeInsets.fromLTRB(16, 4, 8, 4),
                                  trailing: IconButton(
                                    icon: const Icon(Icons.add_circle_outline, color: AppColors.signalBlue),
                                    onPressed: state.busyWithAction
                                        ? null
                                        : () async {
                                            final error = await state.linkAsset(asset.id);
                                            if (error != null && sheetContext.mounted) {
                                              ScaffoldMessenger.of(sheetContext).showSnackBar(SnackBar(content: Text(error)));
                                            }
                                          },
                                  ),
                                ),
                              );
                            },
                          );
                        },
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<TechnicianLinkedAssetsState>(
      builder: (context, state, _) {
        if (state.loading && state.linked.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.linked.isEmpty) {
          return Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
            ),
          );
        }
        return Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              FilledButton.icon(
                onPressed: state.busyWithAction ? null : () => _showLinkAssetSheet(context, state),
                icon: const Icon(Icons.link, size: 18),
                label: const Text('Vincular activo'),
              ),
              const SizedBox(height: 16),
              Expanded(
                child: state.linked.isEmpty
                    ? const Center(
                        child: Text('Este técnico no tiene activos vinculados todavía.\nNo verá equipos en "Lectura de contadores".',
                            textAlign: TextAlign.center, style: TextStyle(color: AppColors.inkSecondary)),
                      )
                    : ListView.builder(
                        itemCount: state.linked.length,
                        itemBuilder: (context, index) {
                          final item = state.linked[index];
                          return ClayCard(
                            child: Row(
                              children: [
                                const ClayIconBadge(icon: Icons.print_outlined, color: AppColors.signalBlue),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('${item.assetBrandName} ${item.model}', style: const TextStyle(fontWeight: FontWeight.w600)),
                                      Text(
                                        '${item.clientName ?? 'Sin cliente'}'
                                        '${item.area != null && item.area!.isNotEmpty ? ' · ${item.area}' : ''}\n'
                                        'Serie: ${item.serialNumber}'
                                        '${item.cityName != null ? ' · ${item.cityName}' : ''}',
                                        style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
                                      ),
                                    ],
                                  ),
                                ),
                                IconButton(
                                  icon: const Icon(Icons.delete_outline, color: AppColors.signalRed),
                                  onPressed: state.busyWithAction ? null : () => state.unlinkAsset(item.id),
                                ),
                              ],
                            ),
                          );
                        },
                      ),
              ),
            ],
          ),
        );
      },
    );
  }
}
