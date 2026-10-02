import 'package:flutter/foundation.dart';

import '../models/technician.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/technician_management_api.dart';

class TechniciansState extends ChangeNotifier {
  TechniciansState(ApiClient client) : _api = TechnicianManagementApi(client) {
    // Disponibilidad, cobertura y estado del técnico cambian solos (check-in/out, fuera de la oficina).
    _unsubscribe = RealtimeService.instance.subscribe([
      'Technician',
    ], (_) => load(silent: true));
  }

  final TechnicianManagementApi _api;
  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  String? error;
  List<Technician> technicians = [];

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      technicians = await _api.list();
    } catch (e, st) {
      debugPrint('TechniciansState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar los técnicos.';
      }
    } finally {
      loading = false;
      notifyListeners();
    }
  }
}
