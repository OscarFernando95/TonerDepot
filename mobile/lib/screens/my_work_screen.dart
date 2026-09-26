import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/my_work_state.dart';
import '../theme/app_theme.dart';
import '../widgets/animated_gradient_border.dart';
import '../widgets/checkout_sheet.dart';
import '../widgets/installation_card.dart';
import '../widgets/order_card.dart';
import '../widgets/ticket_card.dart';

/// Contenido de "Mi trabajo" — sin Scaffold/AppBar propios, porque ahora
/// vive como una de las tres pestañas del shell con barra lateral
/// (ver MainShell). El AppBar y el logout son compartidos entre pestañas.
class MyWorkScreen extends StatefulWidget {
  const MyWorkScreen({super.key});

  @override
  State<MyWorkScreen> createState() => _MyWorkScreenState();
}

class _MyWorkScreenState extends State<MyWorkScreen> {
  late final MyWorkState _state;
  String? _actingOnId;

  @override
  void initState() {
    super.initState();
    _state = context.read<MyWorkState>();
  }

  Future<void> _handleCheckIn(Future<String?> Function() action, String id) async {
    setState(() => _actingOnId = id);
    final error = await action();
    if (!mounted) return;
    setState(() => _actingOnId = null);
    if (error != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
    } else {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Check-in registrado.')));
    }
  }

  Future<void> _handleCheckOut(MyWorkState state) async {
    final request = await CheckoutSheet.show(
      context,
      activeTicket: state.activeTicket,
      activeOrder: state.activeOrder,
      activeInstallation: state.activeInstallation,
    );
    if (request == null || !mounted) return;

    final error = await state.checkOut(request);
    if (!mounted) return;
    if (error != null) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(request.resolved ? 'Check-out registrado. Trabajo resuelto.' : 'Check-out registrado.')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<MyWorkState>(
      builder: (context, state, _) {
        if (state.loading && state.tickets.isEmpty && state.orders.isEmpty) {
          return const Center(child: CircularProgressIndicator());
        }
        return RefreshIndicator(
          onRefresh: state.loadAll,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (state.error != null)
                Card(
                  color: AppColors.signalRedWash,
                  shape: const RoundedRectangleBorder(side: BorderSide(color: AppColors.signalRed)),
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Text(state.error!, style: const TextStyle(color: AppColors.signalRed)),
                  ),
                ),
              if (state.isBusy) _ActiveVisitCard(state: state, onCheckOut: () => _handleCheckOut(state)),
              const SizedBox(height: 16),
              Text('Mis tickets (${state.checkInableTickets.length})', style: Theme.of(context).textTheme.titleSmall),
              if (state.tickets.isEmpty) const Padding(padding: EdgeInsets.all(8), child: Text('No tienes tickets asignados.')),
              ...state.tickets.map(
                (t) => TicketCard(
                  ticket: t,
                  canCheckIn: !state.isBusy && (t.status == 'Asignado' || t.status == 'EnProceso'),
                  isActive: state.activeTicket?.id == t.id,
                  checkingIn: _actingOnId == t.id,
                  onCheckIn: () => _handleCheckIn(() => _state.checkInTicket(t), t.id),
                ),
              ),
              const SizedBox(height: 16),
              Text('Mis órdenes de mantenimiento (${state.checkInableOrders.length})',
                  style: Theme.of(context).textTheme.titleSmall),
              if (state.orders.isEmpty) const Padding(padding: EdgeInsets.all(8), child: Text('No tienes órdenes asignadas.')),
              ...state.orders.map(
                (o) => OrderCard(
                  order: o,
                  canCheckIn: !state.isBusy && (o.status == 'Asignada' || o.status == 'EnProceso'),
                  isActive: state.activeOrder?.id == o.id,
                  checkingIn: _actingOnId == o.id,
                  onCheckIn: () => _handleCheckIn(() => _state.checkInOrder(o), o.id),
                ),
              ),
              const SizedBox(height: 16),
              Text('Instalaciones pendientes (${state.pendingInstallations.length})',
                  style: Theme.of(context).textTheme.titleSmall),
              if (state.pendingInstallations.isEmpty)
                const Padding(padding: EdgeInsets.all(8), child: Text('No hay instalaciones pendientes.')),
              ...state.pendingInstallations.map(
                (i) => InstallationCard(
                  installation: i,
                  canCheckIn: !state.isBusy,
                  isActive: state.activeInstallation?.assetId == i.assetId,
                  checkingIn: _actingOnId == i.assetId,
                  onCheckIn: () => _handleCheckIn(() => _state.checkInInstallation(i), i.assetId),
                ),
              ),
              const SizedBox(height: 32),
            ],
          ),
        );
      },
    );
  }
}

class _ActiveVisitCard extends StatelessWidget {
  const _ActiveVisitCard({required this.state, required this.onCheckOut});

  final MyWorkState state;
  final VoidCallback onCheckOut;

  @override
  Widget build(BuildContext context) {
    final ticket = state.activeTicket;
    final order = state.activeOrder;
    final installation = state.activeInstallation;
    return Card(
      color: AppColors.signalBlueWash,
      shape: const RoundedRectangleBorder(side: BorderSide(color: AppColors.signalBlueBright)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Visita en curso', style: TextStyle(fontWeight: FontWeight.bold, color: AppColors.signalBlueBright)),
            const SizedBox(height: 8),
            if (ticket != null) Text('${ticket.clientName} — ${ticket.clientLocationName}\n${ticket.description}'),
            if (order != null) Text('${order.assetBrandName} ${order.assetModel} — ${order.assetSerialNumber}'),
            if (installation != null)
              Text('${installation.clientName} — ${installation.clientLocationName}\n'
                  '${installation.assetBrandName} ${installation.model} — ${installation.serialNumber}'),
            const SizedBox(height: 12),
            AnimatedGradientBorder(
              backgroundColor: AppColors.signalBlueWash,
              child: SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: state.busyWithAction ? null : onCheckOut,
                  icon: const Icon(Icons.logout, size: 18),
                  label: const Text('Check-out'),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
