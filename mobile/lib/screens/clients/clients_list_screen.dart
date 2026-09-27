import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/client.dart';
import '../../services/api_client.dart';
import '../../state/clients_list_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';

/// Espejo de ClientsListView.vue.
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
              final created = await context.push<bool>('/clients/new');
              if (created == true && context.mounted) {
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
                    child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed), textAlign: TextAlign.center),
                  ),
                );
              }
              if (state.clients.isEmpty) {
                return const PlaceholderScreen(title: 'Sin clientes', message: 'Todavía no hay clientes registrados.');
              }
              return RefreshIndicator(
                onRefresh: state.load,
                child: ListView.builder(
                  padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
                  itemCount: state.clients.length,
                  itemBuilder: (context, index) => _ClientItem(client: state.clients[index]),
                ),
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
              Expanded(child: Text(client.name, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15))),
              StatusChip.activeState(client.isActive),
            ],
          ),
          if (client.taxId != null) ...[
            const SizedBox(height: 4),
            Text('NIT: ${client.taxId}', style: const TextStyle(color: AppColors.inkSecondary)),
          ],
          const SizedBox(height: 6),
          Text(
            '${client.locationCount} sede(s)${client.cityNames.isEmpty ? '' : ' — ${client.cityNames.join(', ')}'}',
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          if (!client.isContractClient) ...[
            const SizedBox(height: 4),
            const Text('Cliente externo (sin equipos catalogados)', style: TextStyle(fontSize: 11, fontStyle: FontStyle.italic)),
          ],
        ],
      ),
    );
  }
}
