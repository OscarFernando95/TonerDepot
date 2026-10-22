import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../models/client_home.dart';
import '../../models/client_home_text.dart';
import '../../services/api_client.dart';
import '../../state/auth_state.dart';
import '../../state/client_home_state.dart';
import '../../theme/app_theme.dart';
import '../tickets/ticket_create_sheet.dart';
import 'client_home_cards.dart';

/// Ancho a partir del cual Tickets y Mantenimientos (y luego Instalaciones y Máquinas) van en fila.
const double _wideBreakpoint = 600;

/// Tope de ancho del contenido, para que en una tableta las tarjetas no se estiren sin sentido.
const double _maxContentWidth = 920;

/// Inicio del portal Cliente: tarjetas con el estado de su servicio — tickets, mantenimientos, instalaciones y sus
/// máquinas — y un botón grande para reportar un problema. Se actualiza solo con el realtime (ClientHomeState).
class ClientPortalHomeScreen extends StatelessWidget {
  const ClientPortalHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => ClientHomeState(ApiClient.instance)..load(),
      child: const _ClientHomeBody(),
    );
  }
}

class _ClientHomeBody extends StatelessWidget {
  const _ClientHomeBody();

  Future<void> _report(BuildContext context) async {
    final state = context.read<ClientHomeState>();
    final messenger = ScaffoldMessenger.of(context);
    final created = await TicketCreateSheet.show(context);
    if (created != true) return;
    messenger.showSnackBar(
      const SnackBar(
        content: Text('Reportamos tu problema. Lo verás aquí en un momento.'),
      ),
    );
    state.load(silent: true);
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<ClientHomeState>();
    final auth = context.watch<AuthState>();
    final home = state.home;
    final name = greetingName(
      home?.clientName ?? '',
      auth.currentUser?.fullName,
    );

    return RefreshIndicator(
      onRefresh: () => state.load(silent: true),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final wide = constraints.maxWidth >= _wideBreakpoint;
          return ListView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: const EdgeInsets.fromLTRB(16, 20, 16, 28),
            children: [
              Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: _maxContentWidth),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _Greeting(name: name),
                      const SizedBox(height: 16),
                      FilledButton.icon(
                        onPressed: () => _report(context),
                        style: FilledButton.styleFrom(
                          minimumSize: const Size.fromHeight(56),
                        ),
                        icon: const Icon(Icons.add_circle_outline),
                        label: const Text(
                          'Reportar un problema',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      const SizedBox(height: 18),
                      if (home != null)
                        _Cards(home: home, wide: wide)
                      else if (state.error != null && !state.loading)
                        HomeErrorCard(
                          message: state.error,
                          onRetry: () => state.load(),
                        )
                      else
                        const HomeSkeleton(),
                    ],
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _Greeting extends StatelessWidget {
  const _Greeting({required this.name});
  final String name;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Hola,',
          style: TextStyle(color: AppColors.inkSecondary, fontSize: 14),
        ),
        if (name.isNotEmpty)
          Semantics(
            header: true,
            child: Text(
              name,
              style: Theme.of(context).textTheme.headlineSmall
                  ?.copyWith(fontWeight: FontWeight.bold),
            ),
          ),
      ],
    );
  }
}

/// Las cuatro tarjetas, sin encabezados de bloque. En teléfono van en una columna; en pantallas anchas se
/// emparejan: Tickets con Mantenimientos, Instalaciones con Máquinas.
class _Cards extends StatelessWidget {
  const _Cards({required this.home, required this.wide});

  final ClientHome home;
  final bool wide;

  @override
  Widget build(BuildContext context) {
    final tickets = TicketsCard(
      home: home,
      onOpenList: () => context.go('/tickets'),
      onOpenTicket: (id) => context.push('/tickets/$id'),
    );
    final maintenance = MaintenanceCard(
      home: home,
      onOpen: () => context.go('/my-maintenance'),
    );
    final installations = InstallationsCard(
      home: home,
      onOpen: () => context.go('/my-assets'),
    );
    final machines = MachinesCard(
      home: home,
      onOpen: () => context.go('/my-assets'),
    );

    const gap = 14.0;
    if (!wide) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          tickets,
          const SizedBox(height: gap),
          maintenance,
          const SizedBox(height: gap),
          installations,
          const SizedBox(height: gap),
          machines,
        ],
      );
    }
    Widget pair(Widget a, Widget b) => IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Expanded(child: a),
          const SizedBox(width: gap),
          Expanded(child: b),
        ],
      ),
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        pair(tickets, maintenance),
        const SizedBox(height: gap),
        pair(installations, machines),
      ],
    );
  }
}
