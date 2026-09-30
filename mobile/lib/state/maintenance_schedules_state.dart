import 'package:flutter/foundation.dart';

import '../models/contract.dart';
import '../models/grouping.dart';
import '../models/maintenance_schedule.dart';
import '../services/api_client.dart';
import '../services/contract_api.dart';
import '../services/maintenance_schedule_api.dart';

const _noCity = 'Sin ciudad';

class MaintenanceSchedulesState extends ChangeNotifier {
  MaintenanceSchedulesState(ApiClient client)
    : _api = MaintenanceScheduleApi(client),
      _contractApi = ContractApi(client);

  final MaintenanceScheduleApi _api;
  final ContractApi _contractApi;
  static const _pageSize = 50;

  bool loading = false;
  bool loadingMore = false;
  bool hasMore = false;
  bool busyWithAction = false;
  String? error;
  List<MaintenanceSchedule> schedules = [];
  /// Solo para armar la etiqueta "Cliente (vigencia)" del filtro de contrato
  /// — espejo de `contracts`/`contractLabelById` en MaintenanceSchedulesView.vue.
  List<Contract> contracts = [];
  int _page = 1;
  String? urgencyFilter;
  String? cityFilter;
  String? clientFilter;
  String? contractFilter;

  /// 'grouped' | 'flat' — espejo de viewMode en MaintenanceSchedulesView.vue
  /// (ahí también arranca en 'grouped').
  String viewMode = 'grouped';

  /// `exclude` es la clave del propio filtro que está construyendo sus
  /// opciones — se omite a sí mismo para que, al elegir ciudad, cliente y
  /// contrato sigan ofreciendo las opciones de ESA ciudad (espejo exacto de
  /// `matches(s, exclude)` en MaintenanceSchedulesView.vue). La urgencia no
  /// es un filtro de la web (es exclusivo de la app), así que siempre se
  /// aplica — nunca se excluye a sí misma.
  bool _matches(MaintenanceSchedule s, String exclude) {
    final urgencyOk = urgencyFilter == null || s.urgency == urgencyFilter;
    final cityOk = exclude == 'city' || cityFilter == null || s.cityName == cityFilter;
    final clientOk = exclude == 'client' || clientFilter == null || s.clientId == clientFilter;
    final contractOk = exclude == 'contract' || contractFilter == null || s.contractId == contractFilter;
    return urgencyOk && cityOk && clientOk && contractOk;
  }

  /// Espejo de MaintenanceSchedulesView.vue: filtro client-side sobre lo ya
  /// cargado, urgencia + ciudad + cliente + contrato combinados.
  List<MaintenanceSchedule> get filtered =>
      schedules.where((s) => _matches(s, '')).toList();

  /// Opciones de ciudad/cliente/contrato en cascada: cada una se calcula
  /// sobre los cronogramas que ya cumplen los OTROS filtros elegidos —
  /// elegir una ciudad reduce cliente y contrato a los de esa ciudad, igual
  /// que cityOptions/clientOptions/contractOptions en la web.
  List<String> get cityOptions => ({
    for (final s in schedules)
      if (s.cityName != null && _matches(s, 'city')) s.cityName!,
  }.toList())..sort();

  List<(String, String)> get clientOptions {
    final seen = <String, String>{};
    for (final s in schedules) {
      if (_matches(s, 'client')) seen[s.clientId] = s.clientName;
    }
    final list = seen.entries.map((e) => (e.key, e.value)).toList();
    list.sort((a, b) => a.$2.compareTo(b.$2));
    return list;
  }

  static String _fmtDate(String iso) => iso.split('T').first;

  List<(String, String)> get contractOptions {
    final labels = <String, String>{
      for (final c in contracts)
        c.id:
            '${c.clientName} (${_fmtDate(c.startDate)} – '
            '${c.endDate != null ? _fmtDate(c.endDate!) : 'indefinida'})',
    };
    final seen = <String>{};
    for (final s in schedules) {
      if (_matches(s, 'contract')) seen.add(s.contractId);
    }
    final list = seen.map((id) => (id, labels[id] ?? id)).toList();
    list.sort((a, b) => a.$2.compareTo(b.$2));
    return list;
  }

  /// Árbol ciudad → cliente → contrato sobre [filtered] — espejo de
  /// groupedByCity en MaintenanceSchedulesView.vue. A diferencia de Activos,
  /// un cronograma siempre nace con cliente y contrato (se crea al instalar
  /// un activo bajo un contrato), así que no hay caso especial tipo "Bodega".
  List<CityGroup<MaintenanceSchedule>> get groupedByCity =>
      groupByCityClientContract<MaintenanceSchedule>(
        filtered,
        city: (s) => s.cityName ?? _noCity,
        clientKey: (s) => s.clientId,
        clientLabel: (s) => s.clientName,
        contractKey: (s) => s.contractId,
        contractLabel: (s) => _contractLabel(s.contractId),
      );

  /// Reutiliza la misma lista de [contracts] que ya se carga para
  /// `contractOptions` — no dispara una segunda llamada al catálogo.
  String _contractLabel(String contractId) {
    Contract? contract;
    for (final c in contracts) {
      if (c.id == contractId) {
        contract = c;
        break;
      }
    }
    if (contract == null) return contractId;
    final end = contract.endDate == null
        ? 'indefinida'
        : _fmtDate(contract.endDate!);
    return '${contract.clientName} (${_fmtDate(contract.startDate)} – $end)';
  }

  void setViewMode(String mode) {
    viewMode = mode;
    notifyListeners();
  }

  Future<void> load() async {
    loading = true;
    error = null;
    _page = 1;
    notifyListeners();
    try {
      final page = await _api.list(page: _page, pageSize: _pageSize);
      schedules = page.items;
      hasMore = page.hasMore;
      try {
        final contractsPage = await _contractApi.listCatalog(
          page: 1,
          pageSize: 200,
        );
        contracts = contractsPage.items;
      } catch (e, st) {
        // Solo alimenta la etiqueta bonita del filtro de contrato — si falla
        // no rompemos la carga de cronogramas, el filtro sigue funcionando
        // (con el id crudo como etiqueta).
        debugPrint('MaintenanceSchedulesState.load (contracts) failed: $e\n$st');
      }
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.load failed: $e\n$st');
      error = e is ApiException
          ? e.message
          : 'No se pudieron cargar los cronogramas.';
    } finally {
      loading = false;
      notifyListeners();
    }
  }

  Future<void> loadMore() async {
    if (loadingMore || !hasMore) return;
    loadingMore = true;
    notifyListeners();
    try {
      final page = await _api.list(page: _page + 1, pageSize: _pageSize);
      _page += 1;
      schedules = [...schedules, ...page.items];
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.loadMore failed: $e\n$st');
    } finally {
      loadingMore = false;
      notifyListeners();
    }
  }

  void setUrgencyFilter(String? urgency) {
    urgencyFilter = urgency;
    notifyListeners();
  }

  void setCityFilter(String? city) {
    cityFilter = city;
    notifyListeners();
  }

  void setClientFilter(String? clientId) {
    clientFilter = clientId;
    notifyListeners();
  }

  void setContractFilter(String? contractId) {
    contractFilter = contractId;
    notifyListeners();
  }

  /// Devuelve el mensaje a mostrar (éxito o error) para que la pantalla solo
  /// tenga que pintarlo en un SnackBar.
  Future<String> backfill() async {
    busyWithAction = true;
    notifyListeners();
    try {
      final created = await _api.backfill();
      await load();
      return created > 0
          ? 'Se crearon $created cronograma(s) nuevo(s).'
          : 'No había activos instalados sin cronograma.';
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.backfill failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo completar la regeneración.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }

  Future<String> evaluateNow() async {
    busyWithAction = true;
    notifyListeners();
    try {
      final created = await _api.evaluateNow();
      await load();
      return created > 0
          ? 'Se generaron $created orden(es) de mantenimiento.'
          : 'Ningún cronograma está por vencer todavía.';
    } catch (e, st) {
      debugPrint('MaintenanceSchedulesState.evaluateNow failed: $e\n$st');
      return e is ApiException
          ? e.message
          : 'No se pudo evaluar los cronogramas.';
    } finally {
      busyWithAction = false;
      notifyListeners();
    }
  }
}
