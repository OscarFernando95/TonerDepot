import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/asset.dart';
import '../../models/status_labels.dart';
import '../../services/api_client.dart';
import '../../state/assets_list_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de AssetsListView.vue — GET /assets no tiene filtros server-side,
/// así que el filtro por estado es client-side sobre lo ya cargado (ver
/// AssetsListState, que sí pagina de verdad para no truncar el catálogo).
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
                  child: SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: Row(
                      children: [
                        ChoiceChip(label: const Text('Todos'), selected: state.statusFilter == null, onSelected: (_) => state.setFilter(null)),
                        for (final status in StatusLabels.assetLifecycle.keys)
                          Padding(
                            padding: const EdgeInsets.only(left: 6),
                            child: ChoiceChip(
                              label: Text(StatusLabels.assetLifecycle[status]!),
                              selected: state.statusFilter == status,
                              onSelected: (_) => state.setFilter(status),
                            ),
                          ),
                      ],
                    ),
                  ),
                ),
                Expanded(
                  child: Builder(
                    builder: (context) {
                      if (state.loading && state.assets.isEmpty) {
                        return const Center(child: CircularProgressIndicator());
                      }
                      if (state.error != null && state.assets.isEmpty) {
                        return Center(
                          child: Padding(
                            padding: const EdgeInsets.all(24),
                            child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
                          ),
                        );
                      }
                      final assets = state.filtered;
                      if (assets.isEmpty) {
                        return const PlaceholderScreen(title: 'Sin activos', message: 'No hay activos que coincidan con el filtro.');
                      }
                      return RefreshIndicator(
                        onRefresh: state.load,
                        child: NotificationListener<ScrollNotification>(
                          onNotification: (notification) {
                            if (notification.metrics.pixels >= notification.metrics.maxScrollExtent - 200) {
                              state.loadMore();
                            }
                            return false;
                          },
                          child: ListView.builder(
                            padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
                            itemCount: assets.length + (state.hasMore ? 1 : 0),
                            itemBuilder: (context, index) {
                              if (index >= assets.length) {
                                return const Padding(
                                  padding: EdgeInsets.symmetric(vertical: 16),
                                  child: Center(child: CircularProgressIndicator()),
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

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: () => context.push('/assets/${asset.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text('${asset.assetBrandName} ${asset.model}', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15))),
              StatusChip.assetLifecycle(asset.lifecycleStatus),
            ],
          ),
          const SizedBox(height: 4),
          Text('Serie: ${asset.serialNumber}', style: const TextStyle(color: AppColors.inkSecondary)),
          if (asset.currentClientName != null)
            Text(
              '${asset.currentClientName}${asset.currentClientLocationName != null ? ' — ${asset.currentClientLocationName}' : ''}',
              style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
            ),
        ],
      ),
    );
  }
}
