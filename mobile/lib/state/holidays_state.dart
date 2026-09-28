import 'package:flutter/foundation.dart';

import '../models/technician_schedule.dart';
import '../services/api_client.dart';
import '../services/holiday_api.dart';

class HolidaysState extends ChangeNotifier {
  HolidaysState(ApiClient client) : _api = HolidayApi(client);

  final HolidayApi _api;

  bool loading = false;
  String? error;
  int year = DateTime.now().year;
  List<Holiday> holidays = [];

  Future<void> load([int? newYear]) async {
    if (newYear != null) year = newYear;
    loading = true;
    error = null;
    notifyListeners();
    try {
      holidays = await _api.list(year);
    } catch (e, st) {
      debugPrint('HolidaysState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los festivos.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
