import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../models/technician_home.dart';
import '../services/api_client.dart';
import '../state/auth_state.dart';
import '../state/my_work_state.dart';
import '../state/technician_home_state.dart';
import '../theme/app_theme.dart';
import '../widgets/clay_icon_badge.dart';
import '../widgets/clay_surface.dart';

/// Contenido de "/dashboard" para el rol Tecnico (ver DashboardHomeScreen). Mismo contenido que el Inicio del técnico
/// en la web (DashboardView.vue): estado y jornada, visita en curso o siguiente trabajo, agenda, resumen del día y
/// stock bajo de su zona — todo de un solo llamado (GET /technicians/me/home).
class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (_) => TechnicianHomeState(ApiClient.instance)..load(),
      child: const _HomeBody(),
    );
  }
}

class _HomeBody extends StatelessWidget {
  const _HomeBody();

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthState>();
    final state = context.watch<TechnicianHomeState>();
    final work = context.watch<MyWorkState>();
    final home = state.home;
    final availableInstallations = work.pendingInstallations
        .where((i) => !i.takenByAnotherTechnician)
        .length;

    return RefreshIndicator(
      onRefresh: () async {
        await Future.wait([state.load(silent: true), work.loadAll()]);
      },
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          const Text(
            'Hola,',
            style: TextStyle(color: AppColors.inkSecondary, fontSize: 14),
          ),
          Text(
            auth.currentUser?.fullName ?? '',
            style: Theme.of(context).textTheme.headlineSmall
                ?.copyWith(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 12),
          if (home == null && state.loading)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 48),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (home == null)
            _ErrorCard(message: state.error, onRetry: state.load)
          else ...[
            _StatusCard(home: home),
            const SizedBox(height: 12),
            if (home.activeVisit != null)
              _ActiveVisitCard(
                job: home.activeVisit!,
                startedAt: home.activeVisitStartedAt,
              )
            else if (home.nextJob != null)
              _NextJobCard(job: home.nextJob!)
            else
              const _EmptyAgendaCard(),
            const SizedBox(height: 12),
            _TodayTiles(home: home, installations: availableInstallations),
            if (home.restOfAgenda.isNotEmpty) ...[
              const SizedBox(height: 20),
              const _SectionTitle('Agenda'),
              const SizedBox(height: 8),
              for (final job in home.restOfAgenda) _AgendaRow(job: job),
            ],
            if (home.lowStock.isNotEmpty) ...[
              const SizedBox(height: 20),
              _LowStockCard(items: home.lowStock),
            ],
          ],
          const SizedBox(height: 24),
          FilledButton.icon(
            onPressed: () => context.push('/my-work'),
            icon: const Icon(Icons.work_outline),
            label: const Text('Ir a Mi trabajo'),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => context.push('/meter-readings'),
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColors.inkPrimary,
              side: const BorderSide(color: AppColors.neutral),
              shape: const RoundedRectangleBorder(),
              padding: const EdgeInsets.symmetric(vertical: 14),
            ),
            icon: const Icon(Icons.speed_outlined),
            label: const Text('Lectura de contador y tóner'),
          ),
        ],
      ),
    );
  }
}

Color _priorityColor(String priority) => switch (priority) {
  'Critica' => AppColors.signalRed,
  'Alta' => AppColors.signalAmber,
  'Baja' => AppColors.neutral,
  _ => AppColors.signalBlueBright,
};

String _priorityLabel(String priority) =>
    priority == 'Critica' ? 'Crítica' : priority;

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);
  final String text;

  @override
  Widget build(BuildContext context) => Text(
    text.toUpperCase(),
    style: const TextStyle(
      color: AppColors.inkSecondary,
      fontWeight: FontWeight.w600,
      fontSize: 11,
      letterSpacing: 0.6,
    ),
  );
}

class _ErrorCard extends StatelessWidget {
  const _ErrorCard({required this.message, required this.onRetry});
  final String? message;
  final Future<void> Function({bool silent}) onRetry;

  @override
  Widget build(BuildContext context) => ClaySurface(
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          message ?? 'No se pudo cargar el inicio.',
          style: const TextStyle(color: AppColors.signalRed),
        ),
        const SizedBox(height: 8),
        OutlinedButton(
          onPressed: () => onRetry(),
          child: const Text('Reintentar'),
        ),
      ],
    ),
  );
}

/// Zona, si está en horario o en permiso, y su jornada de hoy.
class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.home});
  final TechnicianHome home;

  @override
  Widget build(BuildContext context) {
    final onLeave = home.timeOffUntil != null;
    final (color, label) = onLeave
        ? (
            AppColors.signalAmber,
            'En permiso hasta ${_dayAndTime(home.timeOffUntil!)}',
          )
        : home.isWorkingNow
        ? (AppColors.signalBlueBright, 'En horario')
        : (AppColors.neutral, 'Fuera de horario');

    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _StatusDot(color: color, blink: false),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  label,
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            home.todayShift != null
                ? 'Jornada de hoy: ${home.todayShift}'
                : 'Hoy no es día laboral para ti',
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 13),
          ),
          if (home.zoneNames.isNotEmpty) ...[
            const SizedBox(height: 4),
            Text(
              'Zona: ${home.zoneNames.join(', ')}',
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 13,
              ),
            ),
          ] else ...[
            const SizedBox(height: 4),
            const Text(
              'Aún no tienes una zona asignada.',
              style: TextStyle(color: AppColors.signalAmber, fontSize: 13),
            ),
          ],
        ],
      ),
    );
  }

  static String _dayAndTime(DateTime d) =>
      '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')} '
      '${d.hour.toString().padLeft(2, '0')}:${d.minute.toString().padLeft(2, '0')}';
}

class _PriorityTag extends StatelessWidget {
  const _PriorityTag(this.priority);
  final String priority;

  @override
  Widget build(BuildContext context) {
    final color = _priorityColor(priority);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        border: Border.all(color: color.withValues(alpha: 0.6)),
      ),
      child: Text(
        _priorityLabel(priority).toUpperCase(),
        style: TextStyle(
          color: color,
          fontWeight: FontWeight.w700,
          fontSize: 10,
          letterSpacing: 0.5,
        ),
      ),
    );
  }
}

Future<void> _openDirections(BuildContext context, HomeJob job) async {
  final uri = directionsUri(job);
  final messenger = ScaffoldMessenger.of(context);
  if (uri == null) {
    messenger.showSnackBar(
      const SnackBar(
        content: Text('Esta sede no tiene dirección ni ubicación.'),
      ),
    );
    return;
  }
  try {
    final opened = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!opened) {
      messenger.showSnackBar(
        const SnackBar(content: Text('No se pudo abrir el mapa.')),
      );
    }
  } catch (e, st) {
    debugPrint('HomeScreen._openDirections failed: $e\n$st');
    messenger.showSnackBar(
      const SnackBar(content: Text('No se pudo abrir el mapa.')),
    );
  }
}

/// Lo que sigue: el trabajo más urgente (prioridad, luego antigüedad) con qué es, dónde y desde cuándo.
class _NextJobCard extends StatelessWidget {
  const _NextJobCard({required this.job});
  final HomeJob job;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const _SectionTitle('Siguiente trabajo'),
              const Spacer(),
              _PriorityTag(job.priority),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              ClayIconBadge(
                icon: job.kind == 'Orden'
                    ? Icons.build_outlined
                    : Icons.confirmation_number_outlined,
                color: _priorityColor(job.priority),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  job.title,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
              ),
            ],
          ),
          if (job.summary != null && job.summary!.isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(job.summary!, style: const TextStyle(fontSize: 13)),
          ],
          const SizedBox(height: 8),
          Text(
            job.placeLabel,
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 13),
          ),
          if (job.address != null && job.address!.isNotEmpty)
            Text(
              job.address!,
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 12,
              ),
            ),
          const SizedBox(height: 4),
          Text(
            '${job.kind} · ${ageLabel(job.since, DateTime.now().toUtc())}',
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed: () => context.push('/my-work'),
                  icon: const Icon(Icons.login, size: 18),
                  label: const Text('Iniciar'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => _openDirections(context, job),
                  icon: const Icon(Icons.directions_outlined, size: 18),
                  label: const Text('Cómo llegar'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// Visita en curso con cronómetro y acceso directo para cerrarla.
class _ActiveVisitCard extends StatefulWidget {
  const _ActiveVisitCard({required this.job, required this.startedAt});
  final HomeJob job;
  final DateTime? startedAt;

  @override
  State<_ActiveVisitCard> createState() => _ActiveVisitCardState();
}

class _ActiveVisitCardState extends State<_ActiveVisitCard> {
  Timer? _ticker;

  @override
  void initState() {
    super.initState();
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final job = widget.job;
    final started = widget.startedAt;
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              _StatusDot(color: AppColors.signalBlueBright, blink: false),
              SizedBox(width: 10),
              _SectionTitle('Visita en curso'),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            job.title,
            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
          ),
          const SizedBox(height: 4),
          Text(
            job.placeLabel,
            style: const TextStyle(color: AppColors.inkSecondary, fontSize: 13),
          ),
          if (started != null) ...[
            const SizedBox(height: 10),
            Semantics(
              label: 'Tiempo de visita',
              child: Text(
                elapsedLabel(DateTime.now().toUtc().difference(started)),
                style: Theme.of(context).textTheme.headlineMedium
                    ?.copyWith(fontWeight: FontWeight.bold)
                    .merge(AppTextStyles.tabularNumber),
              ),
            ),
          ],
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: FilledButton.icon(
                  onPressed: () => context.push('/my-work'),
                  icon: const Icon(Icons.logout, size: 18),
                  label: const Text('Cerrar visita'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => _openDirections(context, job),
                  icon: const Icon(Icons.directions_outlined, size: 18),
                  label: const Text('Mapa'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _EmptyAgendaCard extends StatelessWidget {
  const _EmptyAgendaCard();

  @override
  Widget build(BuildContext context) => const ClaySurface(
    child: Row(
      children: [
        _StatusDot(color: AppColors.neutral, blink: false),
        SizedBox(width: 12),
        Expanded(
          child: Text(
            'No tienes trabajo pendiente por ahora',
            style: TextStyle(fontWeight: FontWeight.w600),
          ),
        ),
      ],
    ),
  );
}

/// Visitas cerradas hoy, horas trabajadas e instalaciones disponibles.
class _TodayTiles extends StatelessWidget {
  const _TodayTiles({required this.home, required this.installations});
  final TechnicianHome home;
  final int installations;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _StatTile(
            label: 'Visitas hoy',
            value: '${home.visitsClosedToday}',
            icon: Icons.task_alt_outlined,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: _StatTile(
            label: 'Trabajado',
            value: workedLabel(home.minutesWorkedToday),
            icon: Icons.schedule_outlined,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: _StatTile(
            label: 'Instalaciones',
            value: '$installations',
            icon: Icons.move_to_inbox_outlined,
            highlight: installations > 0,
          ),
        ),
      ],
    );
  }
}

class _AgendaRow extends StatelessWidget {
  const _AgendaRow({required this.job});
  final HomeJob job;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: InkWell(
        onTap: () => context.push('/my-work'),
        child: ClaySurface(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          child: Row(
            children: [
              Container(
                width: 4,
                height: 36,
                color: _priorityColor(job.priority),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      job.title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    Text(
                      job.placeLabel,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.inkSecondary,
                        fontSize: 12,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Text(
                ageLabel(job.since, DateTime.now().toUtc()),
                style: const TextStyle(
                  color: AppColors.inkSecondary,
                  fontSize: 11,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Piezas con stock bajo en las zonas del técnico: para pedir reposición antes de salir.
class _LowStockCard extends StatelessWidget {
  const _LowStockCard({required this.items});
  final List<HomeLowStock> items;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              ClayIconBadge(
                icon: Icons.warning_amber_rounded,
                color: AppColors.signalAmber,
                size: 28,
                iconSize: 14,
              ),
              SizedBox(width: 10),
              Expanded(child: _SectionTitle('Stock bajo en tu zona')),
            ],
          ),
          const SizedBox(height: 8),
          for (final item in items)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 3),
              child: Row(
                children: [
                  Expanded(child: Text(item.itemName)),
                  Text(
                    item.isNegative
                        ? '${item.quantity} (negativo)'
                        : '${item.quantity} / mín. ${item.minimumStock}',
                    style: TextStyle(
                      color: item.isNegative
                          ? AppColors.signalRed
                          : AppColors.signalAmber,
                      fontWeight: FontWeight.w600,
                      fontSize: 13,
                    ),
                  ),
                ],
              ),
            ),
          const SizedBox(height: 4),
          const Text(
            'Avisa para que repongan antes de tu próxima visita.',
            style: TextStyle(color: AppColors.inkSecondary, fontSize: 12),
          ),
        ],
      ),
    );
  }
}

class _StatTile extends StatelessWidget {
  const _StatTile({
    required this.label,
    required this.value,
    required this.icon,
    this.highlight = false,
  });
  final String label;
  final String value;
  final IconData icon;
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    return ClaySurface(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClayIconBadge(
            icon: icon,
            color: highlight ? AppColors.signalAmber : AppColors.neutral,
            size: 28,
            iconSize: 14,
          ),
          const SizedBox(height: 8),
          Text(
            label.toUpperCase(),
            style: const TextStyle(
              color: AppColors.inkSecondary,
              fontSize: 10,
              letterSpacing: 0.5,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            value,
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.bold)
                .merge(AppTextStyles.tabularNumber),
          ),
        ],
      ),
    );
  }
}

/// Lámpara de estado del panel "Inicio": cuadrado de color que nunca se
/// apaga del todo (piso alto de opacidad) más un resplandor (glow) que
/// respira al mismo ritmo — se lee como una luz encendida, no como un
/// simple parpadeo débil. `blink: false` la deja fija (para "en curso", que
/// es un estado calmado, no una alerta) y `blink: true` la hace parpadear
/// (para "pendiente sin iniciar", que sí pide atención).
class _StatusDot extends StatefulWidget {
  const _StatusDot({required this.color, required this.blink});

  final Color color;
  final bool blink;

  @override
  State<_StatusDot> createState() => _StatusDotState();
}

class _StatusDotState extends State<_StatusDot>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 500),
    );
    if (widget.blink) {
      _controller.repeat(reverse: true);
    } else {
      _controller.value = 1;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) {
        // t oscila entre 0.55 y 1.0 (o queda fijo en 1.0 si no parpadea) —
        // nunca se apaga del todo, para que se vea "brillante" y no débil.
        final t = 0.55 + (0.45 * _controller.value);
        return Container(
          width: 12,
          height: 12,
          decoration: BoxDecoration(
            color: widget.color.withValues(alpha: t),
            boxShadow: [
              BoxShadow(
                color: widget.color.withValues(alpha: t * 0.85),
                blurRadius: 10 * t,
                spreadRadius: 2 * t,
              ),
            ],
          ),
        );
      },
    );
  }
}
