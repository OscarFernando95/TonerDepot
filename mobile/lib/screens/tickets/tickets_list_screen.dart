import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/service_ticket.dart';
import '../../services/api_client.dart';
import '../../state/auth_state.dart';
import '../../state/tickets_list_state.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';
import '../common/placeholder_screen.dart';
import 'ticket_create_sheet.dart';

/// Lista de tickets para Cliente/Administrador/Coordinador — espejo de
/// TicketsListView.vue. El FAB de crear solo se muestra para Cliente por
/// ahora: crear como Staff necesita elegir cliente+sede (requiere ClientApi,
/// que todavía no existe — llega junto con el catálogo de clientes de la
/// Fase F). Staff mientras tanto ve la lista en modo solo lectura.
class TicketsListScreen extends StatelessWidget {
  const TicketsListScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TicketsListState(ApiClient.instance)..load(),
      child: const _TicketsListBody(),
    );
  }
}

class _TicketsListBody extends StatelessWidget {
  const _TicketsListBody();

  @override
  Widget build(BuildContext context) {
    final canCreate = context.watch<AuthState>().currentUser?.clientId != null;
    return Scaffold(
      backgroundColor: Colors.transparent,
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(
              onPressed: () async {
                final created = await TicketCreateSheet.show(context);
                if (created == true && context.mounted) {
                  context.read<TicketsListState>().load();
                }
              },
              icon: const Icon(Icons.add),
              label: const Text('Reportar'),
            )
          : null,
      body: Consumer<TicketsListState>(
        builder: (context, state, _) {
          if (state.loading && state.tickets.isEmpty) {
            return const Center(child: CircularProgressIndicator());
          }
          if (state.error != null && state.tickets.isEmpty) {
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
          if (state.tickets.isEmpty) {
            return const PlaceholderScreen(
              title: 'Sin tickets',
              message: 'No hay tickets para mostrar todavía.',
            );
          }
          return RefreshIndicator(
            onRefresh: state.load,
            child: ListView.builder(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 96),
              itemCount: state.tickets.length,
              itemBuilder: (context, index) =>
                  _TicketListItem(ticket: state.tickets[index]),
            ),
          );
        },
      ),
    );
  }
}

class _TicketListItem extends StatelessWidget {
  const _TicketListItem({required this.ticket});

  final ServiceTicket ticket;

  @override
  Widget build(BuildContext context) {
    return ClayCard(
      onTap: () => context.push('/tickets/${ticket.id}'),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  ticket.clientName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 15,
                  ),
                ),
              ),
              StatusChip.priority(ticket.priority),
              const SizedBox(width: 6),
              StatusChip(value: ticket.status),
            ],
          ),
          const SizedBox(height: 4),
          Text(
            ticket.clientLocationName,
            style: const TextStyle(color: AppColors.inkSecondary),
          ),
          const SizedBox(height: 6),
          Text(
            ticket.description,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
          if (ticket.technicianName != null) ...[
            const SizedBox(height: 4),
            Text(
              'Técnico: ${ticket.technicianName}',
              style: const TextStyle(
                fontSize: 12,
                color: AppColors.inkSecondary,
              ),
            ),
          ],
        ],
      ),
    );
  }
}
