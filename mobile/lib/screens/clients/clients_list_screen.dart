import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/client.dart';
import '../../services/api_client.dart';
import '../../state/clients_list_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/list_filter_dropdown.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de ClientsListView.vue — incluye el filtro por ciudad
/// (client-side sobre lo ya cargado, igual que la web).
class ClientsListScreen extends StatelessWidget {
  const ClientsListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => ClientsListState(ApiClient.instance)..load(),
      // Builder para que el `context` de abajo (FAB incluido) sí vea el
      // ClientsListState que se acaba de crear — el `context` del propio
      // build() de este StatelessWidget es su ancestro, no su descendiente.
      child: Builder(
        builder: (context) => Scaffold(
          backgroundColor: Colors.transparent,
          floatingActionButton: FloatingActionButton.extended(
            onPressed: () async {
              final created = await context.push<Client>('/clients/new');
              if (created != null && context.mounted) {
                context.read<ClientsListState>().load();
              }
            },
            icon: const Icon(Icons.add),
            label: const Text('Cliente'),
          ),
          body: Consumer<ClientsListState>(
            builder: (context, state, _) {
              if (state.loading && state.clients.isEmpty) {
                return const Center(child: CircularProgressIndicator());
              }
              if (state.error != null && state.clients.isEmpty) {
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
              if (state.clients.isEmpty) {
                return const PlaceholderScreen(
                  title: 'Sin clientes',
                  message: 'Todavía no hay clientes registrados.',
                );
              }
              final clients = state.filtered;
              return Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
                    child: SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      child: Row(
                        children: [
                          ListFilterDropdown(
                            label: 'Ciudad',
                            value: state.cityFilter,
                            options: [
                              for (final c in state.cityOptions) (c, c),
                            ],
                            onChanged: state.setCityFilter,
                          ),
                        ],
                      ),
                    ),
                  ),
                  Expanded(
                    child: clients.isEmpty
                        ? const PlaceholderScreen(
                            title: 'Sin clientes',
                            message: 'No hay clientes en esa ciudad.',
                          )
                        : RefreshIndicator(
                            onRefresh: state.load,
                            child: ListView.builder(
                              padding: const EdgeInsets.fromLTRB(
                                16,
                                4,
                                16,
                                96,
                              ),
                              itemCount: clients.length,
                              itemBuilder: (context, index) =>
                                  _ClientItem(client: clients[index]),
                            ),
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

class _ClientItem extends StatelessWidget {
  const _ClientItem({required this.client});

  final Client client;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: () => context.push('/clients/${client.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              ClayIconBadge(
                icon: Icons.business_outlined,
                color: client.isActive
                    ? AppColors.signalBlue
                    : AppColors.neutral,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  client.name,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip.activeState(client.isActive),
            ],
          ),
          if (client.taxId != null) ...[
            const SizedBox(height: 4),
            Text(
              'NIT: ${client.taxId}',
              style: const TextStyle(color: AppColors.inkSecondary),
            ),
          ],
          const SizedBox(height: 6),
          Text(
            '${client.locationCount} sede(s)${client.cityNames.isEmpty ? '' : ' — ${client.cityNames.join(', ')}'}',
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          if (client.supportCoverage == 'Continuo24x7') ...[
            const SizedBox(height: 4),
            const Text(
              'Soporte 24/7',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: AppColors.signalBlue,
              ),
            ),
          ],
          if (!client.isContractClient) ...[
            const SizedBox(height: 4),
            const Text(
              'Cliente externo (sin equipos catalogados)',
              style: TextStyle(fontSize: 11, fontStyle: FontStyle.italic),
            ),
          ],
        ],
      ),
    );
  }
}
