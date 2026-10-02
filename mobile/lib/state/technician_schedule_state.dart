import 'package:flutter/foundation.dart';

import '../models/technician_schedule.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/technician_management_api.dart';

/// Edita el horario semanal de un técnico. `days` es una copia editable por día (0 = domingo);
/// solo se persiste al llamar save().
class TechnicianScheduleState extends ChangeNotifier {
  TechnicianScheduleState(ApiClient client, this.technicianId)
    : _api = TechnicianManagementApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Technician'], (e) {
      if (saving || _dirty) return;
      load(silent: true);
    });
  }

  final TechnicianManagementApi _api;
  final String technicianId;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  /// Hay cambios locales sin guardar: una recarga silenciosa no debe pisarlos.
  bool _dirty = false;

  bool loading = false;
  bool saving = false;
  String? error;
  bool isDefault = true;
  String timeZoneId = '';
  final Map<int, List<WorkInterval>> days = {for (var d = 0; d < 7; d++) d: []};

  bool get hasAnyInterval => days.values.any((l) => l.isNotEmpty);

  void _fill(TechnicianSchedule schedule) {
    _dirty = false;
    isDefault = schedule.isDefault;
    timeZoneId = schedule.timeZoneId;
    for (final list in days.values) {
      list.clear();
    }
    for (final i in schedule.intervals) {
      days[i.day]!.add(WorkInterval(day: i.day, start: i.start, end: i.end));
    }
    for (final list in days.values) {
      list.sort((a, b) => a.start.compareTo(b.start));
    }
  }

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      _fill(await _api.getSchedule(technicianId));
    } catch (e, st) {
      debugPrint('TechnicianScheduleState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException ? e.message : 'No se pudo cargar el horario.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  void addInterval(int day) {
    final list = days[day]!;
    list.add(
      list.isEmpty
          ? WorkInterval(day: day, start: '08:00', end: '17:00')
          : WorkInterval(day: day, start: list.last.end, end: '18:00'),
    );
    _dirty = true;
    notifyListeners();
  }

  void removeInterval(int day, int index) {
    days[day]!.removeAt(index);
    _dirty = true;
    notifyListeners();
  }

  void setTime(WorkInterval interval, {String? start, String? end}) {
    if (start != null) interval.start = start;
    if (end != null) interval.end = end;
    _dirty = true;
    notifyListeners();
  }

  /// null si guardó; si no, el mensaje de error.
  Future<String?> save() => _run(
    () async => _fill(
      await _api.setSchedule(technicianId, [for (final l in days.values) ...l]),
    ),
  );

  Future<String?> resetToDefault() =>
      _run(() async => _fill(await _api.resetSchedule(technicianId)));

  Future<String?> _run(Future<void> Function() action) async {
    saving = true;
    notifyListeners();
    try {
      await action();
      return null;
    } catch (e, st) {
      debugPrint('TechnicianScheduleState action failed: $e\n$st');
      return e is ApiException ? e.message : 'No se pudo completar la acción.';
    } finally {
      saving = false;
      notifyListeners();
    }
  }
}
