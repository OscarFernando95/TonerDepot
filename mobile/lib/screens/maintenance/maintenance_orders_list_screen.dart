import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/maintenance_order.dart';
import '../../services/api_client.dart';
import '../../state/maintenance_orders_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de MaintenanceOrdersView.vue — el backend ya devuelve todas las
/// órdenes para Staff, el filtro de estado es 100% client-side (igual que
/// en la web, que solo tiene una tabla `sortable`).
class MaintenanceOrdersListScreen extends StatelessWidget {
  const MaintenanceOrdersListScreen({super.key});

  static const _statuses = [
    'Pendiente',
    'Asignada',
    'EnProceso',
    'Completada',
    'Cancelada',
  ];

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => MaintenanceOrdersState(ApiClient.instance)..load(),
      child: Consumer<MaintenanceOrdersState>(
        builder: (context, state, _) {
          return Column(
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                child: SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    children: [
                      ChoiceChip(
                        label: const Text('Todas'),
                        selected: state.statusFilter == null,
                        onSelected: (_) => state.setFilter(null),
                      ),
                      for (final status in _statuses)
                        Padding(
                          padding: const EdgeInsets.only(left: 6),
                          child: ChoiceChip(
                            label: Text(status),
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
                    final orders = state.filtered;
                    if (orders.isEmpty) {
                      return const PlaceholderScreen(
                        title: 'Sin órdenes',
                        message: 'No hay órdenes que coincidan con el filtro.',
                      );
                    }
                    return RefreshIndicator(
                      onRefresh: state.load,
                      child: ListView.builder(
                        padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
                        itemCount: orders.length,
                        itemBuilder: (context, index) =>
                            _OrderListItem(order: orders[index]),
                      ),
                    );
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _OrderListItem extends StatelessWidget {
  const _OrderListItem({required this.order});

  final MaintenanceOrder order;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: () => context.push('/maintenance-orders/${order.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  '${order.assetBrandName} ${order.assetModel}',
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip(value: order.status),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Serie: ${order.assetSerialNumber}',
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          if (order.clientLocationName != null)
            Text(
              '${order.clientLocationName}${order.cityName != null ? ' — ${order.cityName}' : ''}',
              style: const TextStyle(color: AppColors.inkSecondary),
            ),
          const SizedBox(height: 4),
          Text(
            'Programada: ${order.scheduledDate.split('T').first}',
            style: const TextStyle(fontSize: 12),
          ),
          if (order.technicianName != null)
            Text(
              'Técnico: ${order.technicianName}',
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.inkSecondary,
              ),
            ),
        ],
      ),
    );
  }
}
