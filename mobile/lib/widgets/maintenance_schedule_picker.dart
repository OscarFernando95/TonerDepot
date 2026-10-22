import 'package:flutter/material.dart';

import '../models/maintenance_thread_text.dart';

/// El técnico elige día y hora (locales) para el mantenimiento. Devuelve el instante local elegido, o null si
/// canceló o la combinación no sirve (pasado o a más de 365 días; en ese caso avisa con un SnackBar). Quien llama
/// lo envía al servidor como ISO UTC.
Future<DateTime?> pickMaintenanceSchedule(
  BuildContext context, {
  DateTime? current,
}) async {
  final now = DateTime.now();
  final today = DateTime(now.year, now.month, now.day);
  final currentLocal = current?.toLocal();
  final initial = (currentLocal != null && currentLocal.isAfter(now))
      ? currentLocal
      : now.add(const Duration(hours: 1));
  final date = await showDatePicker(
    context: context,
    helpText: 'Día del mantenimiento',
    initialDate: DateTime(initial.year, initial.month, initial.day),
    firstDate: today,
    lastDate: today.add(const Duration(days: maintenanceScheduleMaxDays)),
  );
  if (date == null || !context.mounted) return null;
  final time = await showTimePicker(
    context: context,
    helpText: 'Hora del mantenimiento',
    initialTime: TimeOfDay(hour: initial.hour, minute: initial.minute),
  );
  if (time == null || !context.mounted) return null;
  final chosen = combineDateAndTime(date, time.hour, time.minute);
  final invalid = validatePlannedAt(chosen);
  if (invalid != null) {
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(invalid)));
    return null;
  }
  return chosen;
}
