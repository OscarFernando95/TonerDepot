/// Lógica pura (frases descriptivas, "en orden vs atención", formato de fechas y tiempos) del inicio del Cliente.
/// Sin widgets ni `intl` para que sea testeable y estable, igual que maintenance_thread_text.dart.
library;

import 'client_home.dart';
import 'maintenance_thread_text.dart';
import 'status_labels.dart';

/// Cuánto pide atención una tarjeta. La pantalla lo traduce a color, pero el texto siempre lo dice también (nunca
/// color solo).
enum HomeAccent {
  /// No hay nada de esto: tarjeta tranquila, en gris.
  idle,

  /// Todo bien o avanzando como debe: azul de marca.
  calm,

  /// Hay algo que mirar o a lo que se espera respuesta: ámbar.
  attention,

  /// Algo crítico abierto: rojo suave.
  urgent,
}

/// Texto corto que acompaña a la "lámpara" de la tarjeta, para que el estado nunca dependa solo del color.
String accentLabel(HomeAccent accent) => switch (accent) {
  HomeAccent.idle => '',
  HomeAccent.calm => 'Al día',
  HomeAccent.attention => 'Atención',
  HomeAccent.urgent => 'Urgente',
};

/// Cuántas filas de cada lista se muestran en su tarjeta.
const int homeTicketRows = 3;
const int homeMaintenanceRows = 3;
const int homeInstallationRows = 3;

/// "1 ticket" / "3 tickets".
String pluralEs(int n, String one, String many) => n == 1 ? one : many;

/// Nombre que saluda: la empresa; si el servidor no la mandó, el nombre del usuario; si tampoco, vacío.
String greetingName(String clientName, String? fallbackUserName) {
  final company = clientName.trim();
  if (company.isNotEmpty) return company;
  return fallbackUserName?.trim() ?? '';
}

// ── Tickets ─────────────────────────────────────────────────────────────────────────────────────────────────────

/// Sin cerrar, resuelto ni cancelado (misma definición del servidor para "abierto").
bool isOpenTicketStatus(String status) =>
    const {'Abierto', 'SinAsignar', 'Asignado', 'EnProceso'}.contains(status);

/// Rojo si hay un ticket Crítico abierto entre los que se ven; ámbar con cualquier otro abierto; azul si todo está
/// resuelto.
HomeAccent ticketsAccent(ClientHome home) {
  if (home.openTickets <= 0) return HomeAccent.calm;
  final critical = home.recentTickets.any(
    (t) => isOpenTicketStatus(t.status) && t.priority == 'Critica',
  );
  return critical ? HomeAccent.urgent : HomeAccent.attention;
}

/// "ticket abierto" / "tickets abiertos", para ir junto al número grande.
String ticketsCountLabel(int open) =>
    pluralEs(open, 'ticket abierto', 'tickets abiertos');

/// Frase bajo el número: lo que significa para el cliente.
String ticketsSubtitle(ClientHome home) {
  if (home.openTickets <= 0) return 'Todo en orden: no tienes tickets abiertos';
  if (home.ticketsInProgress > 0) {
    return '${home.ticketsInProgress} en curso: ya hay un técnico trabajando';
  }
  return 'Ninguno en curso todavía';
}

/// Filas que se muestran. Con 0 abiertos no se listan los cerrados (el estado vacío ya dice que todo está en orden).
List<ClientHomeTicket> visibleTickets(ClientHome home) {
  if (home.openTickets <= 0) return const [];
  return home.recentTickets.take(homeTicketRows).toList();
}

/// Cuántos tickets abiertos quedan fuera de las filas visibles ("y 2 más").
int hiddenTicketCount(ClientHome home) {
  final extra = home.openTickets - visibleTickets(home).length;
  return extra > 0 ? extra : 0;
}

/// Quién lo atiende, o null si el ticket ya terminó y no hay nada que decir.
String? ticketTechnicianLabel(ClientHomeTicket ticket) {
  final name = ticket.technicianName;
  if (name != null && name.trim().isNotEmpty) return 'Atiende $name';
  return isOpenTicketStatus(ticket.status) ? 'Esperando técnico' : null;
}

/// Etiqueta de criticidad en español ("Crítica" con tilde).
String priorityLabelEs(String priority) =>
    StatusLabels.priority[priority] ?? priority;

/// "hace 5 min", "hace 2 h", "hace 3 días", "hace 2 sem", y pasados ~2 meses, "oct 2026".
String timeAgoEs(DateTime? createdAt, {DateTime? now}) {
  if (createdAt == null) return '';
  final reference = (now ?? DateTime.now()).toUtc();
  final diff = reference.difference(createdAt.toUtc());
  if (diff.isNegative || diff.inMinutes < 1) return 'hace un momento';
  if (diff.inMinutes < 60) return 'hace ${diff.inMinutes} min';
  if (diff.inHours < 24) return 'hace ${diff.inHours} h';
  if (diff.inDays < 7) {
    final d = diff.inDays;
    return 'hace $d ${pluralEs(d, 'día', 'días')}';
  }
  if (diff.inDays < 60) return 'hace ${diff.inDays ~/ 7} sem';
  final local = createdAt.toLocal();
  const months = [
    'ene',
    'feb',
    'mar',
    'abr',
    'may',
    'jun',
    'jul',
    'ago',
    'sep',
    'oct',
    'nov',
    'dic',
  ];
  return '${months[local.month - 1]} ${local.year}';
}

/// Lectura completa de la tarjeta para lectores de pantalla.
String ticketsSemantics(ClientHome home) =>
    'Tickets. ${home.openTickets} ${ticketsCountLabel(home.openTickets)}. '
    '${ticketsSubtitle(home)}. Toca para ver tus tickets.';

// ── Mantenimientos ──────────────────────────────────────────────────────────────────────────────────────────────

/// Azul si todo lo abierto ya tiene visita o está en curso; ámbar si algo espera fecha; gris si no hay nada.
HomeAccent maintenanceAccent(ClientHome home) {
  if (home.openMaintenance <= 0) return HomeAccent.idle;
  return home.maintenanceWithoutVisitDate > 0
      ? HomeAccent.attention
      : HomeAccent.calm;
}

String maintenanceCountLabel(int open) =>
    pluralEs(open, 'mantenimiento', 'mantenimientos');

/// Desglose para chips: solo lo que tiene algo ("2 programados", "1 en curso", "1 esperando fecha").
List<String> maintenanceBreakdown(ClientHome home) => [
  if (home.maintenanceScheduled > 0)
    '${home.maintenanceScheduled} ${pluralEs(home.maintenanceScheduled, 'programado', 'programados')}',
  if (home.maintenanceInProgress > 0) '${home.maintenanceInProgress} en curso',
  if (home.maintenanceWithoutVisitDate > 0)
    '${home.maintenanceWithoutVisitDate} esperando fecha',
];

/// Frase bajo el número.
String maintenanceSubtitle(ClientHome home) {
  if (home.openMaintenance <= 0) {
    return 'Sin mantenimientos programados por ahora';
  }
  if (home.maintenanceWithoutVisitDate > 0 &&
      home.maintenanceWithoutVisitDate == home.openMaintenance) {
    return 'Tu técnico aún debe confirmar la fecha de la visita';
  }
  return 'Revisa cuándo vienen a tus equipos';
}

/// La próxima visita primero y hasta dos más (el servidor ya las manda ordenadas por fecha).
List<ClientHomeMaintenance> visibleMaintenance(ClientHome home) =>
    home.upcomingMaintenance.take(homeMaintenanceRows).toList();

/// "En curso ahora", "Visita: jue 30 oct, 10:00" (hora local) o "Fecha por confirmar".
String visitLabel(ClientHomeMaintenance item, {DateTime? now}) {
  if (item.inProgress) return 'En curso ahora';
  final planned = item.plannedAt;
  if (planned == null) return 'Fecha por confirmar';
  return 'Visita: ${formatDateTimeEsCo(planned, now: now)}';
}

/// "1 comentario" / "3 comentarios"; null si no hay (no se muestra marca).
String? commentsHint(ClientHomeMaintenance item) {
  if (item.commentCount <= 0) return null;
  return '${item.commentCount} ${pluralEs(item.commentCount, 'comentario', 'comentarios')}';
}

String maintenanceSemantics(ClientHome home, {DateTime? now}) {
  final next = home.upcomingMaintenance.isEmpty
      ? null
      : home.upcomingMaintenance.first;
  final parts = maintenanceBreakdown(home);
  return 'Mantenimientos. ${home.openMaintenance} '
      '${maintenanceCountLabel(home.openMaintenance)}. '
      '${parts.isEmpty ? maintenanceSubtitle(home) : parts.join(', ')}. '
      '${next == null ? '' : 'Próxima: ${next.equipment}, ${visitLabel(next, now: now)}. '}'
      'Toca para ver tus mantenimientos.';
}

// ── Instalaciones ───────────────────────────────────────────────────────────────────────────────────────────────

HomeAccent installationsAccent(ClientHome home) {
  if (home.pendingInstallations <= 0) return HomeAccent.idle;
  return HomeAccent.calm;
}

String installationsCountLabel(int pending) =>
    pluralEs(pending, 'instalación pendiente', 'instalaciones pendientes');

String installationsSubtitle(ClientHome home) {
  if (home.pendingInstallations <= 0) return 'Sin instalaciones pendientes';
  if (home.installationsInProgress > 0) {
    final n = home.installationsInProgress;
    return '$n ${pluralEs(n, 'se está instalando', 'se están instalando')} ahora';
  }
  return 'Aún no empiezan: están por instalar en tus sedes';
}

List<ClientHomeInstallation> visibleInstallations(ClientHome home) =>
    home.installations.take(homeInstallationRows).toList();

String installationsSemantics(ClientHome home) =>
    'Instalaciones. ${installationsSubtitle(home)}'
    '${home.pendingInstallations > 0 ? '. ${home.pendingInstallations} ${installationsCountLabel(home.pendingInstallations)}' : ''}. '
    'Toca para ver tus equipos.';

// ── Mis máquinas ────────────────────────────────────────────────────────────────────────────────────────────────

/// Reparto de las máquinas del cliente: en orden vs. las que piden atención.
class MachineHealth {
  const MachineHealth({
    required this.ok,
    required this.withTicket,
    required this.inMaintenance,
  });

  /// Instaladas sin ticket abierto.
  final int ok;

  /// Instaladas con un ticket abierto.
  final int withTicket;

  /// En mantenimiento ahora.
  final int inMaintenance;

  int get total => ok + withTicket + inMaintenance;

  /// Las que piden atención: con ticket abierto o en mantenimiento.
  int get attention => withTicket + inMaintenance;

  bool get allOk => total > 0 && attention == 0;
}

/// "En orden" = instaladas − con ticket abierto (nunca negativo: si el servidor cuenta un ticket sobre una máquina
/// que ya no figura instalada, se recorta a las instaladas para que la suma cuadre).
MachineHealth machineHealth(ClientHome home) {
  final installed = home.installedAssets < 0 ? 0 : home.installedAssets;
  final ticketed = home.assetsWithOpenTicket.clamp(0, installed);
  return MachineHealth(
    ok: installed - ticketed,
    withTicket: ticketed,
    inMaintenance: home.assetsInMaintenance < 0 ? 0 : home.assetsInMaintenance,
  );
}

HomeAccent machinesAccent(ClientHome home) {
  final h = machineHealth(home);
  if (h.total == 0) return HomeAccent.idle;
  return h.attention > 0 ? HomeAccent.attention : HomeAccent.calm;
}

String machinesCountLabel(int total) => pluralEs(total, 'máquina', 'máquinas');

/// Frase bajo el número.
String machinesSubtitle(ClientHome home) {
  final h = machineHealth(home);
  if (h.total == 0) return 'Aún no tienes máquinas instaladas';
  if (h.allOk) {
    return h.total == 1
        ? 'Tu máquina está en orden'
        : 'Todas tus máquinas están en orden';
  }
  return '${h.attention} de ${h.total} '
      '${pluralEs(h.attention, 'necesita atención', 'necesitan atención')}';
}

/// Chips de la tarjeta; solo los que tienen algo. `kind`: ok | ticket | maintenance.
List<({String kind, String label})> machinesChips(ClientHome home) {
  final h = machineHealth(home);
  return [
    if (h.ok > 0) (kind: 'ok', label: '${h.ok} en orden'),
    if (h.withTicket > 0)
      (
        kind: 'ticket',
        label:
            '${h.withTicket} con ${pluralEs(h.withTicket, 'ticket abierto', 'tickets abiertos')}',
      ),
    if (h.inMaintenance > 0)
      (kind: 'maintenance', label: '${h.inMaintenance} en mantenimiento'),
  ];
}

String machinesSemantics(ClientHome home) {
  final h = machineHealth(home);
  final chips = machinesChips(home).map((c) => c.label).join(', ');
  return 'Mis máquinas. ${h.total} ${machinesCountLabel(h.total)}. '
      '${machinesSubtitle(home)}${chips.isEmpty ? '' : '. $chips'}. '
      'Toca para ver tus equipos.';
}
