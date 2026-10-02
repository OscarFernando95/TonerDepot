import 'package:flutter/foundation.dart';

import '../models/technician_schedule.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/holiday_api.dart';

class HolidaysState extends ChangeNotifier {
  HolidaysState(ApiClient client) : _api = HolidayApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['Holiday'], (e) {
      if (saving) return;
      load(null, true);
    });
  }

  final HolidayApi _api;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;
  int year = DateTime.now().year;
  List<Holiday> holidays = [];

  bool saving = false;

  Future<void> load([int? newYear, bool silent = false]) async {
    if (newYear != null) year = newYear;
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      holidays = await _api.list(year);
    } catch (e, st) {
      debugPrint('HolidaysState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar los festivos.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  /// Devuelve null si salió bien, o un mensaje de error.
  Future<String?> addClosure(String date, String name) async {
    saving = true;
    notifyListeners();
    try {
      await _api.setOverride(date, name, false);
      await load();
      return null;
    } catch (e) {
      return e is ApiException ? e.message : 'No se pudo guardar.';
    } finally {
      saving = false;
      notifyListeners();
    }
  }

  Future<String?> markWorking(Holiday holiday) async {
    try {
      await _api.setOverride(holiday.date, holiday.name, true);
      await load();
      return null;
    } catch (e) {
      return e is ApiException ? e.message : 'No se pudo guardar.';
    }
  }

  Future<String?> removeOverride(Holiday holiday) async {
    try {
      await _api.removeOverride(holiday.date);
      await load();
      return null;
    } catch (e) {
      return e is ApiException ? e.message : 'No se pudo quitar el ajuste.';
    }
  }
}
