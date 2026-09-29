import '../services/device_capture.dart';

import 'package:image_picker/image_picker.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/my_work_state.dart';
import '../theme/app_theme.dart';
import '../widgets/animated_gradient_border.dart';
import '../widgets/checkout_sheet.dart';
import '../widgets/clay_surface.dart';
import '../widgets/installation_card.dart';
import '../widgets/order_card.dart';
import '../widgets/ticket_card.dart';

/// Contenido de "/my-work" — sin Scaffold/AppBar propios, porque vive como
/// el `child` de AppShell (ver screens/app_shell.dart), que provee AppBar,
/// Drawer y logout comunes a todas las rutas.
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

  Future<void> _handleCheckIn(
    Future<String?> Function() action,
    String id,
  ) async {
    setState(() => _actingOnId = id);
    final error = await action();
    if (!mounted) return;
    setState(() => _actingOnId = null);
    if (error != null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error)));
    } else {
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Check-in registrado.')));
    }
  }

  /// Tickets y órdenes: la foto "antes" es obligatoria, se toma con la cámara antes de llamar al check-in.
  Future<void> _handleVisitCheckIn(
    Future<String?> Function(XFile photo) action,
    String id,
  ) async {
    final photo = await DeviceCapture.takePhoto();
    if (photo == null) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Necesitas tomar la foto de la falla o del equipo para hacer check-in.',
            ),
          ),
        );
      }
      return;
    }
    await _handleCheckIn(() => action(photo), id);
  }

  Future<void> _handleCheckOut(MyWorkState state) async {
    final submission = await CheckoutSheet.show(
      context,
      activeTicket: state.activeTicket,
      activeOrder: state.activeOrder,
      activeInstallation: state.activeInstallation,
    );
    if (submission == null || !mounted) return;
    final request = submission.request;

    final error = await state.checkOut(request, photo: submission.photo);
    if (!mounted) return;
    if (error != null) {
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(error)));
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            request.resolved
                ? 'Check-out registrado. Trabajo resuelto.'
                : 'Check-out registrado.',
          ),
        ),
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
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: ClaySurface(
                    color: AppColors.signalRedWash,
                    borderColor: AppColors.signalRed,
                    padding: const EdgeInsets.all(12),
                    child: Text(
                      state.error!,
                      style: const TextStyle(color: AppColors.signalRed),
                    ),
                  ),
                ),
              if (state.isBusy)
                _ActiveVisitCard(
                  state: state,
                  onCheckOut: () => _handleCheckOut(state),
                ),
              const SizedBox(height: 16),
              _SectionHeader(title: 'Mis tickets', count: state.checkInableTickets.length),
              if (state.tickets.isEmpty)
                const Padding(padding: EdgeInsets.all(8), child: Text('No tienes tickets asignados.', textAlign: TextAlign.center)),
              ...state.tickets.map(
                (t) => TicketCard(
                  ticket: t,
                  canCheckIn:
                      !state.isBusy &&
                      (t.status == 'Asignado' || t.status == 'EnProceso'),
                  isActive: state.activeTicket?.id == t.id,
                  checkingIn: _actingOnId == t.id,
                  onCheckIn: () => _handleVisitCheckIn(
                    (photo) => _state.checkInTicket(t, photo),
                    t.id,
                  ),
                ),
              ),
              const SizedBox(height: 16),
              _SectionHeader(title: 'Órdenes de mantenimiento', count: state.checkInableOrders.length),
              if (state.orders.isEmpty)
                const Padding(padding: EdgeInsets.all(8), child: Text('No tienes órdenes asignadas.', textAlign: TextAlign.center)),
              ...state.orders.map(
                (o) => OrderCard(
                  order: o,
                  canCheckIn:
                      !state.isBusy &&
                      (o.status == 'Asignada' || o.status == 'EnProceso'),
                  isActive: state.activeOrder?.id == o.id,
                  checkingIn: _actingOnId == o.id,
                  onCheckIn: () => _handleVisitCheckIn(
                    (photo) => _state.checkInOrder(o, photo),
                    o.id,
                  ),
                ),
              ),
              const SizedBox(height: 16),
              _SectionHeader(title: 'Instalaciones pendientes', count: state.pendingInstallations.length),
              if (state.pendingInstallations.isEmpty)
                const Padding(padding: EdgeInsets.all(8), child: Text('No hay instalaciones pendientes.', textAlign: TextAlign.center)),
              ...state.pendingInstallations.map(
                (i) => InstallationCard(
                  installation: i,
                  canCheckIn: !state.isBusy,
                  isActive: state.activeInstallation?.assetId == i.assetId,
                  checkingIn: _actingOnId == i.assetId,
                  onCheckIn: () => _handleCheckIn(
                    () => _state.checkInInstallation(i),
                    i.assetId,
                  ),
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
    return ClaySurface(
      color: AppColors.signalBlueWash,
      borderColor: AppColors.signalBlueBright,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Visita en curso',
            style: TextStyle(
              fontWeight: FontWeight.bold,
              color: AppColors.signalBlueBright,
            ),
          ),
          const SizedBox(height: 8),
          if (ticket != null)
            Text(
              '${ticket.clientName} — ${ticket.clientLocationName}\n${ticket.description}',
            ),
          if (order != null)
            Text(
              '${order.assetBrandName} ${order.assetModel} — ${order.assetSerialNumber}',
            ),
          if (installation != null)
            Text(
              '${installation.clientName} — ${installation.clientLocationName}\n'
              '${installation.assetBrandName} ${installation.model} — ${installation.serialNumber}',
            ),
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
    );
  }
}


/// Encabezado de sección con el mismo lenguaje visual de "caja" que el
/// resto de la app (ClaySurface) — antes era un Text suelto sin diseño,
/// ver ClaySurface/ClayCard (widgets/clay_surface.dart).
class _SectionHeader extends StatelessWidget {
  const _SectionHeader({required this.title, required this.count});

  final String title;
  final int count;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 12),
      child: Center(
        child: Text(
          '$title ($count)',
          textAlign: TextAlign.center,
          style: const TextStyle(fontWeight: FontWeight.w700, letterSpacing: 0.4, fontSize: 13),
        ),
      ),
    );
  }
}

