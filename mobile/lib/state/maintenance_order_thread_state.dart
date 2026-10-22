import 'package:flutter/foundation.dart';

import '../models/maintenance_order_comment.dart';
import '../models/maintenance_thread_text.dart';
import '../services/api_client.dart';
import '../services/maintenance_order_api.dart';
import '../services/realtime_service.dart';

/// Cómo se suscribe el estado al tiempo real; por defecto el canal único de la app. Inyectable para probar sin
/// SignalR.
typedef RealtimeSubscribe = VoidCallback Function(
  List<String> entities,
  EntityChangedHandler handler,
);

/// Hilo de comentarios de UNA orden de mantenimiento. Vive mientras la hoja/pantalla que lo muestra esté abierta.
class MaintenanceOrderThreadState extends ChangeNotifier {
  MaintenanceOrderThreadState(
    this._api,
    this.orderId, {
    RealtimeSubscribe? subscribe,
  }) {
    // Un comentario nuevo (de otro usuario/dispositivo), un cambio de fecha o un resync tras reconexión.
    _unsubscribe = (subscribe ?? RealtimeService.instance.subscribe)(
      ['MaintenanceOrder'],
      (e) {
        if (!e.affects(orderId)) return;
        load(silent: true);
      },
    );
  }

  final MaintenanceOrderApi _api;
  final String orderId;
  late final VoidCallback _unsubscribe;
  bool _disposed = false;

  bool loading = false;
  bool sending = false;

  /// Error de carga (solo se muestra si no hay nada que enseñar).
  String? loadError;

  /// Error del último envío (se muestra junto a la caja de texto).
  String? sendError;
  List<MaintenanceOrderComment> comments = [];

  /// Para cada entrada de [comments]: ¿es una reprogramación? (hay una programación anterior).
  List<bool> get rescheduleFlagsList => rescheduleFlags(comments);

  @override
  void dispose() {
    _disposed = true;
    _unsubscribe();
    super.dispose();
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  Future<void> load({bool silent = false}) async {
    if (!silent) {
      loading = true;
      loadError = null;
      _notify();
    }
    try {
      comments = await _api.listComments(orderId);
      loadError = null;
    } catch (e, st) {
      debugPrint('MaintenanceOrderThreadState.load failed: $e\n$st');
      if (!silent) {
        loadError = e is ApiException
            ? e.message
            : 'No se pudieron cargar los comentarios.';
      }
    } finally {
      loading = false;
      _notify();
    }
  }

  /// Envía [text]. Devuelve true si quedó publicado; si no, deja el motivo en [sendError].
  Future<bool> send(String text) async {
    if (sending) return false;
    final invalid = validateComment(text);
    if (invalid != null) {
      sendError = invalid;
      _notify();
      return false;
    }
    sending = true;
    sendError = null;
    _notify();
    try {
      final created = await _api.addComment(orderId, text);
      // El aviso en tiempo real puede llegar antes que la respuesta: no duplicar.
      if (!comments.any((c) => c.id == created.id)) {
        comments = [...comments, created];
      }
      return true;
    } catch (e, st) {
      debugPrint('MaintenanceOrderThreadState.send failed: $e\n$st');
      sendError = e is ApiException
          ? e.message
          : 'No se pudo enviar el comentario.';
      return false;
    } finally {
      sending = false;
      _notify();
    }
  }
}
