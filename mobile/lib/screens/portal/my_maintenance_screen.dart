import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/maintenance_order.dart';
import '../../models/status_labels.dart';
import '../../services/api_client.dart';
import '../../state/my_maintenance_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Portal Cliente — mantenimientos programados de sus equipos, solo lectura. `MyMaintenanceState` se instancia local
/// a esta pantalla (igual que MyAssetsState): solo la usa esta ruta.
class MyMaintenanceScreen extends StatelessWidget {
  const MyMaintenanceScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => MyMaintenanceState(ApiClient.instance)..load(),
      child: const _MyMaintenanceBody(),
    );
  }
}

class _MyMaintenanceBody extends StatelessWidget {
  const _MyMaintenanceBody();

  @override
  Widget build(BuildContext context) {
    return Consumer<MyMaintenanceState>(
      builder: (context, state, _) {
        if (state.loading && state.orders.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        if (state.error != null && state.orders.isEmpty) {
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
        if (state.orders.isEmpty) {
          // ListView para que el pull-to-refresh también funcione sin datos.
          return RefreshIndicator(
            onRefresh: state.load,
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              children: const [
                SizedBox(
                  height: 420,
                  child: PlaceholderScreen(
                    title: 'Sin mantenimientos',
                    message: 'Cuando se programe un mantenimiento para tus equipos lo verás aquí.',
                  ),
                ),
              ],
            ),
          );
        }
        final upcoming = state.upcoming;
        final finished = state.finished;
        return RefreshIndicator(
          onRefresh: state.load,
          child: ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.all(16),
            children: [
              if (upcoming.isNotEmpty) ...[
                const _SectionTitle('Programados'),
                for (final order in upcoming) _MaintenanceCard(order: order),
                const SizedBox(height: 8),
              ],
              if (finished.isNotEmpty) ...[
                const _SectionTitle('Finalizados'),
                for (final order in finished) _MaintenanceCard(order: order),
              ],
            ],
          ),
        );
      },
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(4, 4, 4, 8),
      child: Text(
        text,
        style: const TextStyle(
          fontWeight: FontWeight.bold,
          fontSize: 13,
          color: AppColors.inkSecondary,
        ),
      ),
    );
  }
}

class _MaintenanceCard extends StatelessWidget {
  const _MaintenanceCard({required this.order});

  final MaintenanceOrder order;

  @override
  Widget build(BuildContext context) {
    final place = [
      order.clientLocationName,
      order.cityName,
    ].whereType<String>().join(' · ');
    final includes = order.includesLabels;
    final done = order.status == 'Completada';
    return ClayCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              ClayIconBadge(
                icon: Icons.build_outlined,
                color: StatusLabels.colorFor(order.status),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  '${order.assetBrandName} ${order.assetModel}',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip.clientOrder(order.status),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Serie: ${order.assetSerialNumber}',
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          if (place.isNotEmpty)
            Text(place, style: const TextStyle(color: AppColors.inkSecondary)),
          const SizedBox(height: 6),
          Text(
            'Fecha programada: ${MaintenanceOrder.formatScheduledDate(order.scheduledDate)}',
            style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
          ),
          if (done && order.completedAt != null)
            Text(
              'Realizada: ${MaintenanceOrder.formatScheduledDate(order.completedAt!)}',
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
          if (order.technicianName != null)
            Text(
              'Técnico: ${order.technicianName}',
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
          if (includes.isNotEmpty)
            Text(
              'Incluye: ${includes.join(', ')}',
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
