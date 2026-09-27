import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/contract.dart';
import '../../services/api_client.dart';
import '../../state/contracts_list_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de ContractsListView.vue — GET /contracts no tiene filtros
/// server-side, igual patrón que Activos: pagina de verdad y filtra estado
/// client-side.
class ContractsListScreen extends StatelessWidget {
  const ContractsListScreen({super.key});

  static const _statuses = ['Activo', 'Vencido', 'Cancelado'];

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => ContractsListState(ApiClient.instance)..load(),
      // Builder para que el `context` de abajo (FAB incluido) sí vea el
      // ContractsListState que se acaba de crear.
      child: Builder(
        builder: (context) => Scaffold(
        backgroundColor: Colors.transparent,
        floatingActionButton: FloatingActionButton.extended(
          onPressed: () async {
            final created = await context.push<bool>('/contracts/new');
            if (created == true && context.mounted) {
              context.read<ContractsListState>().load();
            }
          },
          icon: const Icon(Icons.add),
          label: const Text('Contrato'),
        ),
        body: Consumer<ContractsListState>(
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
                        for (final status in _statuses)
                          Padding(
                            padding: const EdgeInsets.only(left: 6),
                            child: ChoiceChip(label: Text(status), selected: state.statusFilter == status, onSelected: (_) => state.setFilter(status)),
                          ),
                      ],
                    ),
                  ),
                ),
                Expanded(
                  child: Builder(
                    builder: (context) {
                      if (state.loading && state.contracts.isEmpty) {
                        return const Center(child: CircularProgressIndicator());
                      }
                      if (state.error != null && state.contracts.isEmpty) {
                        return Center(
                          child: Padding(
                            padding: const EdgeInsets.all(24),
                            child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
                          ),
                        );
                      }
                      final contracts = state.filtered;
                      if (contracts.isEmpty) {
                        return const PlaceholderScreen(title: 'Sin contratos', message: 'No hay contratos que coincidan con el filtro.');
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
                            itemCount: contracts.length + (state.hasMore ? 1 : 0),
                            itemBuilder: (context, index) {
                              if (index >= contracts.length) {
                                return const Padding(padding: EdgeInsets.symmetric(vertical: 16), child: Center(child: CircularProgressIndicator()));
                              }
                              return _ContractItem(contract: contracts[index]);
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

class _ContractItem extends StatelessWidget {
  const _ContractItem({required this.contract});

  final Contract contract;

  String _formatDate(String iso) => iso.split('T').first;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: () => context.push('/contracts/${contract.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(contract.clientName, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15))),
              StatusChip.contract(contract.status),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            'Vigencia: ${_formatDate(contract.startDate)}${contract.endDate != null ? ' a ${_formatDate(contract.endDate!)}' : ' (sin fecha de fin)'}',
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          Text(
            '${contract.assetCount} equipo(s)${contract.cityNames.isEmpty ? '' : ' — ${contract.cityNames.join(', ')}'}',
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
        ],
      ),
    );
  }
}
