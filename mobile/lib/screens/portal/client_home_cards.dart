import 'package:flutter/material.dart';

import '../../models/client_home.dart';
import '../../models/client_home_text.dart';
import '../../models/status_labels.dart';
import '../../theme/app_theme.dart';
import '../../widgets/clay_icon_badge.dart';
import '../../widgets/clay_surface.dart';
import '../../widgets/status_chip.dart';

// ── Color según lo que pide atención ──────────────────────────────────────────────────────────────────────────────

/// Color de relleno (lámpara, borde, barra).
Color accentColor(HomeAccent accent) => switch (accent) {
  HomeAccent.idle => AppColors.neutral,
  HomeAccent.calm => AppColors.signalBlue,
  HomeAccent.attention => AppColors.signalAmber,
  HomeAccent.urgent => AppColors.signalRed,
};

/// Versión con contraste suficiente (>= 4.5:1 sobre la tarjeta) para texto e íconos: el ámbar y el rojo de las
/// lámparas son demasiado claros para letra.
Color accentTextColor(HomeAccent accent) => switch (accent) {
  HomeAccent.idle => AppColors.inkSecondary,
  HomeAccent.calm => AppColors.signalBlue,
  HomeAccent.attention => const Color(0xFF8A5A00),
  HomeAccent.urgent => const Color(0xFFB02A2A),
};

const _tabular = AppTextStyles.tabularNumber;

// ── Piezas comunes ────────────────────────────────────────────────────────────────────────────────────────────────

/// Tarjeta clay tocable. Sin Material/InkWell (igual que ClayCard): feedback por escala. La semántica la pone
/// quien la usa, porque unas tarjetas son un solo botón y otras tienen filas que llevan a otro lado.
class _TapCard extends StatefulWidget {
  const _TapCard({
    required this.accent,
    required this.onTap,
    required this.child,
  });

  final HomeAccent accent;
  final VoidCallback onTap;
  final Widget child;

  @override
  State<_TapCard> createState() => _TapCardState();
}

class _TapCardState extends State<_TapCard> {
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    final loud =
        widget.accent == HomeAccent.attention ||
        widget.accent == HomeAccent.urgent;
    return GestureDetector(
      excludeFromSemantics: true,
      behavior: HitTestBehavior.opaque,
      onTapDown: (_) => setState(() => _pressed = true),
      onTapCancel: () => setState(() => _pressed = false),
      onTapUp: (_) => setState(() => _pressed = false),
      onTap: widget.onTap,
      child: AnimatedScale(
        scale: _pressed ? 0.985 : 1.0,
        duration: const Duration(milliseconds: 100),
        child: ClaySurface(
          padding: const EdgeInsets.all(18),
          borderColor: loud
              ? accentColor(widget.accent).withValues(alpha: 0.5)
              : null,
          child: widget.child,
        ),
      ),
    );
  }
}

/// Tarjeta que es un solo botón para lectores de pantalla.
class _WholeCard extends StatelessWidget {
  const _WholeCard({
    required this.accent,
    required this.onTap,
    required this.semanticsLabel,
    required this.child,
  });

  final HomeAccent accent;
  final VoidCallback onTap;
  final String semanticsLabel;
  final Widget child;

  @override
  Widget build(BuildContext context) => Semantics(
    container: true,
    button: true,
    label: semanticsLabel,
    onTap: onTap,
    child: ExcludeSemantics(
      child: _TapCard(accent: accent, onTap: onTap, child: child),
    ),
  );
}

/// Ícono + nombre de la tarjeta + lámpara de estado (forma + texto, nunca color solo).
class _CardHeader extends StatelessWidget {
  const _CardHeader({
    required this.icon,
    required this.title,
    required this.accent,
  });

  final IconData icon;
  final String title;
  final HomeAccent accent;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        ClayIconBadge(
          icon: icon,
          color: accentTextColor(accent),
          size: 40,
          iconSize: 21,
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            title.toUpperCase(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: AppColors.inkSecondary,
              fontWeight: FontWeight.w700,
              fontSize: 12,
              letterSpacing: 0.7,
            ),
          ),
        ),
        _AccentLamp(accent: accent),
      ],
    );
  }
}

/// Lámpara de estado: círculo = al día, rombo = atención, anillo = urgente; siempre con su texto.
class _AccentLamp extends StatelessWidget {
  const _AccentLamp({required this.accent});
  final HomeAccent accent;

  @override
  Widget build(BuildContext context) {
    final label = accentLabel(accent);
    if (label.isEmpty) return const SizedBox.shrink();
    final color = accentColor(accent);
    final Widget shape = switch (accent) {
      HomeAccent.urgent => Container(
        width: 10,
        height: 10,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          border: Border.all(color: color, width: 2),
        ),
      ),
      HomeAccent.attention => Transform.rotate(
        angle: 0.785398,
        child: Container(width: 8, height: 8, color: color),
      ),
      _ => Container(
        width: 9,
        height: 9,
        decoration: BoxDecoration(shape: BoxShape.circle, color: color),
      ),
    };
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        shape,
        const SizedBox(width: 6),
        Text(
          label.toUpperCase(),
          style: TextStyle(
            color: accentTextColor(accent),
            fontWeight: FontWeight.w700,
            fontSize: 11,
            letterSpacing: 0.5,
          ),
        ),
      ],
    );
  }
}

/// Número grande (cifras de ancho fijo) + qué cuenta.
class _BigNumber extends StatelessWidget {
  const _BigNumber({required this.value, required this.label});

  final int value;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.baseline,
      textBaseline: TextBaseline.alphabetic,
      children: [
        Text(
          '$value',
          style: _tabular.copyWith(
            fontSize: 46,
            fontWeight: FontWeight.w800,
            height: 1.0,
            color: AppColors.inkPrimary,
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Text(
            label,
            style: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w600,
              color: AppColors.inkPrimary,
            ),
          ),
        ),
      ],
    );
  }
}

class _Subtitle extends StatelessWidget {
  const _Subtitle(this.text);
  final String text;

  @override
  Widget build(BuildContext context) => Text(
    text,
    style: const TextStyle(
      color: AppColors.inkSecondary,
      fontSize: 14,
      height: 1.35,
    ),
  );
}

/// "Ver todos…" al pie de la tarjeta: deja claro que la tarjeta lleva a algo.
class _CardFooter extends StatelessWidget {
  const _CardFooter(this.text);
  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 14),
    child: Row(
      children: [
        Expanded(
          child: Text(
            text,
            style: const TextStyle(
              color: AppColors.signalBlue,
              fontWeight: FontWeight.w700,
              fontSize: 14,
            ),
          ),
        ),
        const Icon(Icons.chevron_right, color: AppColors.signalBlue, size: 22),
      ],
    ),
  );
}

/// Insignia cuadrada con forma + color + texto (mismo lenguaje de StatusChip) para estados que no vienen de un
/// enum del backend.
class _LampChip extends StatelessWidget {
  const _LampChip({required this.label, required this.color});
  final String label;
  final Color color;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
    decoration: BoxDecoration(
      color: AppColors.claySurfaceRaised,
      border: Border.all(color: color.withValues(alpha: 0.9)),
    ),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(width: 8, height: 8, color: color),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            label.toUpperCase(),
            style: TextStyle(
              color: color == AppColors.signalAmber
                  ? accentTextColor(HomeAccent.attention)
                  : color,
              fontWeight: FontWeight.w600,
              fontSize: 11,
              letterSpacing: 0.4,
            ),
          ),
        ),
      ],
    ),
  );
}

/// Fila compacta tocable dentro de una tarjeta (>= 56 dp de alto), con su propia semántica.
class _PressableRow extends StatefulWidget {
  const _PressableRow({
    required this.onTap,
    required this.semanticsLabel,
    required this.child,
  });

  final VoidCallback onTap;
  final String semanticsLabel;
  final Widget child;

  @override
  State<_PressableRow> createState() => _PressableRowState();
}

class _PressableRowState extends State<_PressableRow> {
  bool _pressed = false;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Semantics(
        button: true,
        label: widget.semanticsLabel,
        onTap: widget.onTap,
        excludeSemantics: true,
        child: GestureDetector(
          excludeFromSemantics: true,
          behavior: HitTestBehavior.opaque,
          onTapDown: (_) => setState(() => _pressed = true),
          onTapCancel: () => setState(() => _pressed = false),
          onTapUp: (_) => setState(() => _pressed = false),
          onTap: widget.onTap,
          child: AnimatedContainer(
            duration: const Duration(milliseconds: 100),
            constraints: const BoxConstraints(minHeight: 56),
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
            decoration: BoxDecoration(
              color: _pressed ? AppColors.neutralSoft : AppColors.canvasBg,
              borderRadius: BorderRadius.circular(14),
            ),
            child: widget.child,
          ),
        ),
      ),
    );
  }
}

/// Fila informativa (sin destino propio: toda la tarjeta lleva a la lista).
class _InfoRow extends StatelessWidget {
  const _InfoRow({required this.child, this.tint = AppColors.canvasBg});
  final Widget child;
  final Color tint;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 8),
    child: Container(
      constraints: const BoxConstraints(minHeight: 56),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: tint,
        borderRadius: BorderRadius.circular(14),
      ),
      child: child,
    ),
  );
}

TextStyle _rowTitle() => const TextStyle(
  fontWeight: FontWeight.w700,
  fontSize: 14.5,
  color: AppColors.inkPrimary,
);

TextStyle _rowMeta() =>
    const TextStyle(fontSize: 12.5, color: AppColors.inkSecondary, height: 1.3);

// ── 1. Tickets ────────────────────────────────────────────────────────────────────────────────────────────────────

class TicketsCard extends StatelessWidget {
  const TicketsCard({
    super.key,
    required this.home,
    required this.onOpenList,
    required this.onOpenTicket,
  });

  final ClientHome home;
  final VoidCallback onOpenList;
  final ValueChanged<String> onOpenTicket;

  @override
  Widget build(BuildContext context) {
    final accent = ticketsAccent(home);
    final rows = visibleTickets(home);
    final hidden = hiddenTicketCount(home);
    final empty = home.openTickets <= 0;

    return _TapCard(
      accent: accent,
      onTap: onOpenList,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Semantics(
            container: true,
            button: true,
            label: ticketsSemantics(home),
            onTap: onOpenList,
            excludeSemantics: true,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _CardHeader(
                  icon: Icons.confirmation_number_outlined,
                  title: 'Tickets',
                  accent: accent,
                ),
                const SizedBox(height: 14),
                _BigNumber(
                  value: home.openTickets,
                  label: ticketsCountLabel(home.openTickets),
                ),
                const SizedBox(height: 6),
                if (empty)
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(
                        Icons.check_circle_outline,
                        size: 20,
                        color: AppColors.signalBlue,
                      ),
                      const SizedBox(width: 8),
                      Expanded(child: _Subtitle(ticketsSubtitle(home))),
                    ],
                  )
                else
                  _Subtitle(ticketsSubtitle(home)),
              ],
            ),
          ),
          for (final t in rows) _TicketRow(ticket: t, onOpen: onOpenTicket),
          if (empty)
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: _Subtitle(
                'Si algo falla con un equipo, repórtalo con el botón de arriba.',
              ),
            ),
          Semantics(
            button: true,
            label: hidden > 0
                ? 'Ver los $hidden tickets restantes y el resto de tu historial'
                : 'Ver todos mis tickets',
            onTap: onOpenList,
            excludeSemantics: true,
            child: _CardFooter(
              hidden > 0
                  ? 'Ver todos los tickets (+$hidden)'
                  : 'Ver todos los tickets',
            ),
          ),
        ],
      ),
    );
  }
}

class _TicketRow extends StatelessWidget {
  const _TicketRow({required this.ticket, required this.onOpen});
  final ClientHomeTicket ticket;
  final ValueChanged<String> onOpen;

  @override
  Widget build(BuildContext context) {
    final open = isOpenTicketStatus(ticket.status);
    final tech = ticketTechnicianLabel(ticket);
    final ago = timeAgoEs(ticket.createdAt);
    final place = [
      if (ticket.locationName.isNotEmpty) ticket.locationName,
      if (ago.isNotEmpty) ago,
    ].join(' · ');
    final statusLabel = StatusLabels.ticket[ticket.status] ?? ticket.status;
    final semantics = [
      ticket.equipment,
      if (ticket.locationName.isNotEmpty) ticket.locationName,
      'estado $statusLabel',
      if (open) 'criticidad ${priorityLabelEs(ticket.priority)}',
      ?tech,
      if (ago.isNotEmpty) ago,
    ].join(', ');

    return _PressableRow(
      onTap: () => onOpen(ticket.id),
      semanticsLabel: 'Ticket: $semantics. Toca para ver el detalle.',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            ticket.equipment,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: _rowTitle(),
          ),
          if (place.isNotEmpty)
            Text(
              place,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: _rowMeta(),
            ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 6,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              StatusChip(value: ticket.status),
              if (open)
                _LampChip(
                  label: 'Criticidad ${priorityLabelEs(ticket.priority)}',
                  color: StatusLabels.priorityColor(ticket.priority),
                ),
              if (tech != null) Text(tech, style: _rowMeta()),
            ],
          ),
        ],
      ),
    );
  }
}

// ── 2. Mantenimientos ─────────────────────────────────────────────────────────────────────────────────────────────

class MaintenanceCard extends StatelessWidget {
  const MaintenanceCard({super.key, required this.home, required this.onOpen});

  final ClientHome home;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final accent = maintenanceAccent(home);
    final rows = visibleMaintenance(home);
    final breakdown = maintenanceBreakdown(home);

    return _WholeCard(
      accent: accent,
      onTap: onOpen,
      semanticsLabel: maintenanceSemantics(home),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _CardHeader(
            icon: Icons.build_outlined,
            title: 'Mantenimientos',
            accent: accent,
          ),
          const SizedBox(height: 14),
          _BigNumber(
            value: home.openMaintenance,
            label: maintenanceCountLabel(home.openMaintenance),
          ),
          const SizedBox(height: 6),
          _Subtitle(maintenanceSubtitle(home)),
          if (breakdown.isNotEmpty) ...[
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 6,
              children: [for (final b in breakdown) _SoftPill(b)],
            ),
          ],
          for (var i = 0; i < rows.length; i++)
            _MaintenanceRow(item: rows[i], featured: i == 0),
          const _CardFooter('Ver mis mantenimientos'),
        ],
      ),
    );
  }
}

class _SoftPill extends StatelessWidget {
  const _SoftPill(this.text);
  final String text;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
    decoration: BoxDecoration(
      color: AppColors.canvasBg,
      borderRadius: BorderRadius.circular(999),
    ),
    child: Text(
      text,
      style: const TextStyle(
        fontSize: 12.5,
        fontWeight: FontWeight.w600,
        color: AppColors.inkPrimary,
      ),
    ),
  );
}

class _MaintenanceRow extends StatelessWidget {
  const _MaintenanceRow({required this.item, required this.featured});
  final ClientHomeMaintenance item;
  final bool featured;

  @override
  Widget build(BuildContext context) {
    final noDate = item.plannedAt == null && !item.inProgress;
    final visitColor = item.inProgress
        ? AppColors.signalBlue
        : noDate
        ? accentTextColor(HomeAccent.attention)
        : AppColors.inkPrimary;
    final place = [
      if (item.locationName != null) item.locationName!,
      if (item.technicianName != null) item.technicianName!,
    ].join(' · ');
    final comments = commentsHint(item);

    return _InfoRow(
      tint: featured
          ? AppColors.signalBlue.withValues(alpha: 0.07)
          : AppColors.canvasBg,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (featured) ...[
            Padding(
              padding: const EdgeInsets.only(top: 1),
              child: Icon(
                item.inProgress
                    ? Icons.play_circle_outline
                    : Icons.event_outlined,
                size: 22,
                color: visitColor,
              ),
            ),
            const SizedBox(width: 10),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  visitLabel(item),
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: featured ? 15.5 : 13.5,
                    color: visitColor,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  item.equipment,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: _rowTitle(),
                ),
                if (place.isNotEmpty)
                  Text(
                    place,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: _rowMeta(),
                  ),
                if (comments != null) ...[
                  const SizedBox(height: 4),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(
                        Icons.chat_bubble_outline,
                        size: 14,
                        color: AppColors.signalBlue,
                      ),
                      const SizedBox(width: 5),
                      Flexible(
                        child: Text(
                          comments,
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: AppColors.signalBlue,
                          ),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

// ── 3. Instalaciones ──────────────────────────────────────────────────────────────────────────────────────────────

class InstallationsCard extends StatelessWidget {
  const InstallationsCard({
    super.key,
    required this.home,
    required this.onOpen,
  });

  final ClientHome home;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final accent = installationsAccent(home);

    if (home.pendingInstallations <= 0) {
      // Compacta: no hay nada que mostrar, pero la tarjeta no desaparece (sabe que no se le olvidó nada).
      return _WholeCard(
        accent: accent,
        onTap: onOpen,
        semanticsLabel: installationsSemantics(home),
        child: Row(
          children: [
            ClayIconBadge(
              icon: Icons.move_to_inbox_outlined,
              color: accentTextColor(accent),
              size: 40,
              iconSize: 21,
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                installationsSubtitle(home),
                style: const TextStyle(
                  fontWeight: FontWeight.w600,
                  fontSize: 15,
                  color: AppColors.inkPrimary,
                ),
              ),
            ),
            const Icon(Icons.chevron_right, color: AppColors.inkSecondary),
          ],
        ),
      );
    }

    final rows = visibleInstallations(home);
    return _WholeCard(
      accent: accent,
      onTap: onOpen,
      semanticsLabel: installationsSemantics(home),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _CardHeader(
            icon: Icons.move_to_inbox_outlined,
            title: 'Instalaciones',
            accent: accent,
          ),
          const SizedBox(height: 14),
          _BigNumber(
            value: home.pendingInstallations,
            label: installationsCountLabel(home.pendingInstallations),
          ),
          const SizedBox(height: 6),
          _Subtitle(installationsSubtitle(home)),
          for (final i in rows)
            _InfoRow(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    i.equipment,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: _rowTitle(),
                  ),
                  if (i.locationName != null)
                    Text(
                      i.locationName!,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: _rowMeta(),
                    ),
                  const SizedBox(height: 8),
                  _LampChip(
                    label: i.inProgress ? 'Instalando ahora' : 'Por instalar',
                    color: i.inProgress
                        ? AppColors.signalBlue
                        : AppColors.neutral,
                  ),
                ],
              ),
            ),
          const _CardFooter('Ver mis equipos'),
        ],
      ),
    );
  }
}

// ── 4. Mis máquinas ───────────────────────────────────────────────────────────────────────────────────────────────

const _maintenanceSlate = Color(0xFF64748B);

Color _machineKindColor(String kind) => switch (kind) {
  'ok' => AppColors.signalBlue,
  'ticket' => AppColors.signalAmber,
  _ => _maintenanceSlate,
};

class MachinesCard extends StatelessWidget {
  const MachinesCard({super.key, required this.home, required this.onOpen});

  final ClientHome home;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final accent = machinesAccent(home);
    final health = machineHealth(home);
    final chips = machinesChips(home);

    return _WholeCard(
      accent: accent,
      onTap: onOpen,
      semanticsLabel: machinesSemantics(home),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _CardHeader(
            icon: Icons.print_outlined,
            title: 'Mis máquinas',
            accent: accent,
          ),
          const SizedBox(height: 14),
          _BigNumber(
            value: health.total,
            label: machinesCountLabel(health.total),
          ),
          const SizedBox(height: 6),
          _Subtitle(machinesSubtitle(home)),
          if (health.total > 0) ...[
            const SizedBox(height: 14),
            _SegmentedBar(health: health),
            const SizedBox(height: 12),
            Wrap(
              spacing: 14,
              runSpacing: 8,
              children: [
                for (final c in chips)
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _LegendMark(kind: c.kind),
                      const SizedBox(width: 6),
                      Flexible(
                        child: Text(
                          c.label,
                          style: const TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w600,
                            color: AppColors.inkPrimary,
                          ),
                        ),
                      ),
                    ],
                  ),
              ],
            ),
          ],
          const _CardFooter('Ver mis equipos'),
        ],
      ),
    );
  }
}

/// Marca de la leyenda: círculo = en orden, rombo = con ticket, cuadrado = en mantenimiento.
class _LegendMark extends StatelessWidget {
  const _LegendMark({required this.kind});
  final String kind;

  @override
  Widget build(BuildContext context) {
    final color = _machineKindColor(kind);
    return switch (kind) {
      'ok' => Container(
        width: 10,
        height: 10,
        decoration: BoxDecoration(shape: BoxShape.circle, color: color),
      ),
      'ticket' => Transform.rotate(
        angle: 0.785398,
        child: Container(width: 8, height: 8, color: color),
      ),
      _ => Container(width: 9, height: 9, color: color),
    };
  }
}

/// Barra segmentada: cada tramo es proporcional a las máquinas de ese estado.
class _SegmentedBar extends StatelessWidget {
  const _SegmentedBar({required this.health});
  final MachineHealth health;

  @override
  Widget build(BuildContext context) {
    final parts = <(int, String)>[
      (health.ok, 'ok'),
      (health.withTicket, 'ticket'),
      (health.inMaintenance, 'maintenance'),
    ].where((p) => p.$1 > 0).toList();
    return SizedBox(
      height: 12,
      child: Row(
        children: [
          for (var i = 0; i < parts.length; i++) ...[
            if (i > 0) const SizedBox(width: 3),
            Expanded(
              flex: parts[i].$1,
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: _machineKindColor(parts[i].$2),
                  borderRadius: BorderRadius.circular(6),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

// ── Carga y error ─────────────────────────────────────────────────────────────────────────────────────────────────

/// Marcadores de posición con la forma de las tarjetas mientras llega la primera respuesta. El pulso se apaga si el
/// sistema pide reducir animaciones.
class HomeSkeleton extends StatefulWidget {
  const HomeSkeleton({super.key});

  @override
  State<HomeSkeleton> createState() => _HomeSkeletonState();
}

class _HomeSkeletonState extends State<HomeSkeleton>
    with SingleTickerProviderStateMixin {
  late final AnimationController _pulse = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1100),
  );

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.of(context).disableAnimations) {
      _pulse.stop();
    } else if (!_pulse.isAnimating) {
      _pulse.repeat(reverse: true);
    }
  }

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Semantics(
      label: 'Cargando tu inicio',
      child: ExcludeSemantics(
        child: AnimatedBuilder(
          animation: _pulse,
          builder: (context, _) {
            final opacity = 0.55 + 0.45 * _pulse.value;
            return Opacity(
              opacity: opacity,
              child: Column(
                children: const [
                  _SkeletonCard(height: 200),
                  SizedBox(height: 14),
                  _SkeletonCard(height: 200),
                  SizedBox(height: 14),
                  _SkeletonCard(height: 76, compact: true),
                  SizedBox(height: 14),
                  _SkeletonCard(height: 200),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

class _SkeletonCard extends StatelessWidget {
  const _SkeletonCard({required this.height, this.compact = false});
  final double height;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    Widget bar(double w, double h) => Container(
      width: w,
      height: h,
      decoration: BoxDecoration(
        color: AppColors.neutralSoft,
        borderRadius: BorderRadius.circular(8),
      ),
    );
    return SizedBox(
      height: height,
      child: ClaySurface(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  width: 40,
                  height: 40,
                  decoration: BoxDecoration(
                    color: AppColors.neutralSoft,
                    borderRadius: BorderRadius.circular(13),
                  ),
                ),
                const SizedBox(width: 12),
                bar(110, 12),
              ],
            ),
            if (!compact) ...[
              const SizedBox(height: 18),
              bar(70, 34),
              const SizedBox(height: 12),
              bar(double.infinity, 12),
              const SizedBox(height: 8),
              bar(180, 12),
            ],
          ],
        ),
      ),
    );
  }
}

/// Error amable con salida clara.
class HomeErrorCard extends StatelessWidget {
  const HomeErrorCard({
    super.key,
    required this.message,
    required this.onRetry,
  });

  final String? message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      container: true,
      liveRegion: true,
      child: ClaySurface(
        borderColor: AppColors.signalRed.withValues(alpha: 0.5),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                const ClayIconBadge(
                  icon: Icons.cloud_off_outlined,
                  color: Color(0xFFB02A2A),
                  size: 40,
                  iconSize: 21,
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Text(
                    'No pudimos cargar tu inicio',
                    style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              message ?? 'Revisa tu conexión e inténtalo de nuevo.',
              style: const TextStyle(
                color: AppColors.inkSecondary,
                fontSize: 14,
                height: 1.35,
              ),
            ),
            const SizedBox(height: 14),
            OutlinedButton.icon(
              onPressed: onRetry,
              style: OutlinedButton.styleFrom(
                minimumSize: const Size.fromHeight(48),
              ),
              icon: const Icon(Icons.refresh),
              label: const Text('Reintentar'),
            ),
          ],
        ),
      ),
    );
  }
}
