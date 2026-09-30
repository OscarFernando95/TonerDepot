import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/status_labels.dart';
import '../../services/api_client.dart';
import '../../state/assets_list_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_choice_chip.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_segmented_control.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/grouped_collapse.dart';
import '../../widgets/list_filter_dropdown.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de AssetsListView.vue — GET /assets no tiene filtros server-side,
/// así que el filtro por estado + ciudad + cliente es client-side sobre lo
/// ya cargado (ver AssetsListState, que sí pagina de verdad para no truncar
/// el catálogo).
class AssetsListScreen extends StatelessWidget {
  const AssetsListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => AssetsListState(ApiClient.instance)..load(),
      // Builder para que el `context` de abajo (FAB incluido) sí vea el
      // AssetsListState que se acaba de crear — el `context` del propio
      // build() de este StatelessWidget es su ancestro, no su descendiente.
      child: Builder(
        builder: (context) => Scaffold(
          backgroundColor: Colors.transparent,
          floatingActionButton: FloatingActionButton.extended(
            onPressed: () async {
              final created = await context.push<bool>('/assets/new');
              if (created == true && context.mounted) {
                context.read<AssetsListState>().load();
              }
            },
            icon: const Icon(Icons.add),
            label: const Text('Activo'),
          ),
          body: Consumer<AssetsListState>(
            builder: (context, state, _) {
              return Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                    child: Wrap(
                      spacing: 6,
                      runSpacing: 6,
                      children: [
                        ClayChoiceChip(
                          label: const Text('Todos'),
                          selected: state.statusFilter == null,
                          onSelected: (_) => state.setFilter(null),
                        ),
                        for (final status in StatusLabels.assetLifecycle.keys)
                          ClayChoiceChip(
                            label: Text(
                              StatusLabels.assetLifecycle[status]!,
                            ),
                            selected: state.statusFilter == status,
                            onSelected: (_) => state.setFilter(status),
                          ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 10, 16, 4),
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        ListFilterDropdown(
                          label: 'Ciudad',
                          value: state.cityFilter,
                          options: [
                            for (final c in state.cityOptions) (c, c),
                          ],
                          onChanged: state.setCityFilter,
                        ),
                        ListFilterDropdown(
                          label: 'Cliente',
                          value: state.clientFilter,
                          options: state.clientOptions,
                          onChanged: state.setClientFilter,
                        ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                    child: ClaySegmentedControl<String>(
                      selected: state.viewMode,
                      onChanged: state.setViewMode,
                      segments: const [
                        ClaySegment(value: 'grouped', label: 'Agrupar por ciudad'),
                        ClaySegment(value: 'flat', label: 'Ver como lista'),
                      ],
                    ),
                  ),
                  Expanded(
                    child: Builder(
                      builder: (context) {
                        if (state.loading && state.assets.isEmpty) {
                          return const Center(
                            child: CircularProgressIndicator(),
                          );
                        }
                        if (state.error != null && state.assets.isEmpty) {
                          return Center(
                            child: Padding(
                              padding: const EdgeInsets.all(24),
                              child: Text(
                                state.error!,
                                style: const TextStyle(
                                  color: AppColors.signalRed,
                                ),
                                textAlign: TextAlign.center,
                              ),
                            ),
                          );
                        }
                        final assets = state.filtered;
                        if (assets.isEmpty) {
                          return const PlaceholderScreen(
                            title: 'Sin activos',
                            message:
                                'No hay activos que coincidan con el filtro.',
                          );
                        }
                        if (state.viewMode == 'grouped') {
                          return RefreshIndicator(
                            onRefresh: state.load,
                            child: GroupedCollapseList<Asset>(
                              // 96 abajo: deja espacio para que el FAB
                              // "+ Activo" no quede montado sobre el último
                              // grupo (mismo criterio que ClientsListScreen).
                              padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
                              groups: state.groupedByCity,
                              itemBuilder: (context, asset) =>
                                  _AssetItem(asset: asset),
                            ),
                          );
                        }
                        return RefreshIndicator(
                          onRefresh: state.load,
                          child: NotificationListener<ScrollNotification>(
                            onNotification: (notification) {
                              if (notification.metrics.pixels >=
                                  notification.metrics.maxScrollExtent - 200) {
                                state.loadMore();
                              }
                              return false;
                            },
                            child: ListView.builder(
                              padding: const EdgeInsets.fromLTRB(16, 4, 16, 96),
                              itemCount:
                                  assets.length + (state.hasMore ? 1 : 0),
                              itemBuilder: (context, index) {
                                if (index >= assets.length) {
                                  return const Padding(
                                    padding: EdgeInsets.symmetric(vertical: 16),
                                    child: Center(
                                      child: CircularProgressIndicator(),
                                    ),
                                  );
                                }
                                return _AssetItem(asset: assets[index]);
                              },
                            ),
                          ),
                        );
                      },
                    ),
                  ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}

class _AssetItem extends StatelessWidget {
  const _AssetItem({required this.asset});

  final Asset asset;

  Color get _statusColor {
    switch (asset.lifecycleStatus) {
      case 'Instalado':
        return AppColors.signalBlue;
      case 'EnMantenimiento':
      case 'PendienteInstalacion':
        return AppColors.signalAmber;
      case 'DadoDeBaja':
        return AppColors.signalRed;
      default:
        return AppColors.neutral;
    }
  }

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: () => context.push('/assets/${asset.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              ClayIconBadge(icon: Icons.print_outlined, color: _statusColor),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  '${asset.assetBrandName} ${asset.model}',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip.assetLifecycle(asset.lifecycleStatus),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Serie: ${asset.serialNumber}',
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          if (asset.currentClientName != null)
            Text(
              '${asset.currentClientName}${asset.currentClientLocationName != null ? ' — ${asset.currentClientLocationName}' : ''}',
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
        ],
      ),
    );
  }
}
