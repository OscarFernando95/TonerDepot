/// Lógica pura (textos, formato es-CO, validación) de la programación de un mantenimiento y de su hilo de
/// comentarios. Sin `intl` ni datos de locale para que sea testeable y estable (mismo criterio que
/// rental_retirement.dart).
library;

import 'maintenance_order.dart';
import 'maintenance_order_comment.dart';

/// Largo máximo de un comentario (el servidor rechaza más de 1000).
const int maintenanceCommentMaxLength = 1000;

/// El servidor rechaza programar a más de 365 días.
const int maintenanceScheduleMaxDays = 365;

const _weekdays = ['lun', 'mar', 'mié', 'jue', 'vie', 'sáb', 'dom'];
const _months = [
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

/// "jue 30 oct, 10:00" en hora local, 24 h. Si [now] está en otro año se agrega: "jue 30 oct 2027, 10:00".
String formatDateTimeEsCo(DateTime value, {DateTime? now}) {
  final local = value.toLocal();
  final reference = (now ?? DateTime.now()).toLocal();
  String two(int n) => n.toString().padLeft(2, '0');
  final year = local.year == reference.year ? '' : ' ${local.year}';
  return '${_weekdays[local.weekday - 1]} ${local.day} '
      '${_months[local.month - 1]}$year, ${two(local.hour)}:${two(local.minute)}';
}

/// "Técnico", "Cliente"… tal como se lee en el hilo.
String maintenanceRoleLabel(String role) {
  switch (role) {
    case 'Cliente':
      return 'Cliente';
    case 'Tecnico':
      return 'Técnico';
    case 'Administrador':
      return 'Administrador';
    case 'Coordinador':
      return 'Coordinador';
    default:
      return role;
  }
}

/// "Juan programó el mantenimiento para el jue 30 oct, 10:00" (o "reprogramó" si [isReschedule]).
String scheduleEventText(
  MaintenanceOrderComment entry, {
  bool isReschedule = false,
  DateTime? now,
}) {
  final who = entry.authorName.trim().isEmpty ? 'El técnico' : entry.authorName;
  final verb = isReschedule ? 'reprogramó' : 'programó';
  final planned = entry.plannedAt;
  if (planned == null) return '$who $verb el mantenimiento';
  return '$who $verb el mantenimiento para el ${formatDateTimeEsCo(planned, now: now)}';
}

/// Marca cuáles entradas 'Programacion' son una reprogramación (hay otra anterior en la lista cronológica).
List<bool> rescheduleFlags(List<MaintenanceOrderComment> thread) {
  final flags = <bool>[];
  var seenSchedule = false;
  for (final entry in thread) {
    flags.add(entry.isSchedule && seenSchedule);
    if (entry.isSchedule) seenSchedule = true;
  }
  return flags;
}

/// Texto del botón que abre el hilo: "Comentarios" o "Comentarios (3)".
String commentsButtonLabel(int count) =>
    count > 0 ? 'Comentarios ($count)' : 'Comentarios';

/// Línea de fecha de la tarjeta del cliente. null cuando la orden ya cerró sin fecha (no hay nada que decir).
String? clientPlannedLabel(MaintenanceOrder order, {DateTime? now}) {
  final planned = order.plannedAt;
  if (planned != null) {
    return 'Programado para ${formatDateTimeEsCo(planned, now: now)}';
  }
  return order.isOpen ? 'Aún sin fecha programada' : null;
}

/// Línea de fecha para el técnico y el staff.
String plannedLabelForStaff(MaintenanceOrder order, {DateTime? now}) {
  final planned = order.plannedAt;
  if (planned == null) return 'Sin fecha programada';
  return formatDateTimeEsCo(planned, now: now);
}

/// Texto del botón del técnico: "Programar" la primera vez, "Reprogramar" después.
String scheduleButtonLabel(MaintenanceOrder order) =>
    order.plannedAt == null ? 'Programar' : 'Reprogramar';

/// null si el comentario se puede enviar; si no, el motivo. Se mide el texto recortado, igual que el servidor.
String? validateComment(String text) {
  final trimmed = text.trim();
  if (trimmed.isEmpty) return 'Escribe un comentario.';
  if (trimmed.length > maintenanceCommentMaxLength) {
    return 'El comentario no puede pasar de $maintenanceCommentMaxLength caracteres.';
  }
  return null;
}

bool canSendComment(String text) => validateComment(text) == null;

/// Contador "123/1000" bajo la caja de texto.
String commentCounterText(String text) =>
    '${text.length}/$maintenanceCommentMaxLength';

/// Une el día elegido y la hora elegida (ambos locales) en un solo instante local.
DateTime combineDateAndTime(DateTime date, int hour, int minute) =>
    DateTime(date.year, date.month, date.day, hour, minute);

/// null si [chosen] (hora local) sirve para programar: futura y a no más de 365 días. Si no, el motivo.
String? validatePlannedAt(DateTime chosen, {DateTime? now}) {
  final reference = now ?? DateTime.now();
  if (!chosen.isAfter(reference)) {
    return 'Elige una fecha y hora futuras.';
  }
  if (chosen.isAfter(
    reference.add(const Duration(days: maintenanceScheduleMaxDays)),
  )) {
    return 'La fecha no puede pasar de $maintenanceScheduleMaxDays días.';
  }
  return null;
}

/// ISO 8601 UTC para el cuerpo de POST …/schedule.
String plannedAtToUtcIso(DateTime local) => local.toUtc().toIso8601String();

/// Texto del botón de confirmar al iniciar un mantenimiento propio: el de siempre, o "Programar mantenimiento" si el
/// técnico eligió otra fecha.
String startMaintenanceConfirmLabel(
  DateTime? plannedAt, {
  required String defaultLabel,
}) => plannedAt == null ? defaultLabel : 'Programar mantenimiento';

/// Mensaje al terminar de iniciar (o programar) un mantenimiento propio.
String startMaintenanceDoneMessage(DateTime? plannedAt, {DateTime? now}) =>
    plannedAt == null
    ? 'Mantenimiento listo. Haz el check-in en Mi trabajo.'
    : 'Mantenimiento programado para ${formatDateTimeEsCo(plannedAt, now: now)}. '
          'El cliente verá la fecha.';

/// Campo opcional `plannedAt` del cuerpo de POST /technicians/me/maintenance-orders: ISO UTC, o null (no se envía)
/// cuando el mantenimiento arranca ahora.
String? startMaintenancePlannedAtField(DateTime? plannedAt) =>
    plannedAt == null ? null : plannedAtToUtcIso(plannedAt);

/// Cuerpo de POST /technicians/me/maintenance-orders. `reason` y `plannedAt` solo viajan si hay valor.
Map<String, Object> startMaintenanceBody({
  required String assetId,
  required bool includesGeneral,
  required bool includesUnits,
  required bool includesConsumables,
  String? reason,
  DateTime? plannedAt,
}) {
  final plannedIso = startMaintenancePlannedAtField(plannedAt);
  return {
    'assetId': assetId,
    'includesGeneral': includesGeneral,
    'includesUnits': includesUnits,
    'includesConsumables': includesConsumables,
    'reason': ?reason,
    'plannedAt': ?plannedIso,
  };
}
