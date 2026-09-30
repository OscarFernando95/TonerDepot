import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/status_labels.dart';
import '../../services/api_client.dart';
import '../../state/my_assets_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Portal Cliente — espejo de solo lectura de MyAssetsView.vue. `MyAssetsState`
/// se instancia local a esta pantalla (no vive en el árbol global de
/// providers de main.dart) porque solo la usa esta ruta.
class MyAssetsScreen extends StatelessWidget {
  const MyAssetsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => MyAssetsState(ApiClient.instance)..load(),
      child: const _MyAssetsBody(),
    );
  }
}

class _MyAssetsBody extends StatelessWidget {
  const _MyAssetsBody();

  @override
  Widget build(BuildContext context) {
    return Consumer<MyAssetsState>(
      builder: (context, state, _) {
        if (state.loading && state.assets.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.assets.isEmpty) {
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
        if (state.assets.isEmpty) {
          return const PlaceholderScreen(
            title: 'Sin activos',
            message: 'Todavía no tienes equipos instalados.',
          );
        }
        return RefreshIndicator(
          onRefresh: state.load,
          child: ListView.builder(
            padding: const EdgeInsets.all(16),
            itemCount: state.assets.length,
            itemBuilder: (context, index) {
              final asset = state.assets[index];
              return ClayCard(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        // Mismo ícono/color que _AssetItem en
                        // assets_list_screen.dart (vista de Staff).
                        ClayIconBadge(
                          icon: Icons.print_outlined,
                          color: StatusLabels.assetLifecycleColor(
                            asset.lifecycleStatus,
                          ),
                        ),
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
                    if (asset.currentClientLocationName != null)
                      Text(
                        '${asset.currentClientLocationName}${asset.cityName != null ? ' — ${asset.cityName}' : ''}',
                        style: const TextStyle(color: AppColors.inkSecondary),
                      ),
                    if (asset.area != null)
                      Text(
                        'Área: ${asset.area}',
                        style: const TextStyle(
                          color: AppColors.inkSecondary,
                          fontSize: 12,
                        ),
                      ),
                    if (asset.lastMeterReading != null)
                      Text(
                        'Último contador: ${asset.lastMeterReading}',
                        style: const TextStyle(
                          color: AppColors.inkSecondary,
                          fontSize: 12,
                        ).merge(AppTextStyles.tabularNumber),
                      ),
                  ],
                ),
              );
            },
          ),
        );
      },
    );
  }
}
