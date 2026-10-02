import 'package:flutter/foundation.dart';

import '../models/managed_user.dart';
import '../services/api_client.dart';
import '../services/realtime_service.dart';
import '../services/user_api.dart';

class UsersListState extends ChangeNotifier {
  UsersListState(ApiClient client) : _api = UserApi(client) {
    // Cambios hechos desde otro usuario/dispositivo (o un resync tras reconexión): recarga silenciosa.
    _unsubscribe = RealtimeService.instance.subscribe(['User'], (e) {
      if (loadingMore) return;
      load(silent: true);
    });
  }

  final UserApi _api;
  static const _pageSize = 50;

  late final VoidCallback _unsubscribe;

  @override
  void dispose() {
    _unsubscribe();
    super.dispose();
  }

  bool loading = false;
  bool loadingMore = false;
  bool hasMore = false;
  String? error;
  List<ManagedUser> users = [];
  int _page = 1;
  String? roleFilter;

  List<ManagedUser> get filtered => roleFilter == null
      ? users
      : users.where((u) => u.roleName == roleFilter).toList();

  Future<void> load({bool silent = false}) async {
    _page = 1;
    if (!silent) {
      loading = true;
      error = null;
      notifyListeners();
    }
    try {
      final page = await _api.list(page: _page, pageSize: _pageSize);
      users = page.items;
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('UsersListState.load failed: $e\n$st');
      if (!silent) {
        error = e is ApiException
            ? e.message
            : 'No se pudieron cargar los usuarios.';
      }
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
      users = [...users, ...page.items];
      hasMore = page.hasMore;
    } catch (e, st) {
      debugPrint('UsersListState.loadMore failed: $e\n$st');
    } finally {
      loadingMore = false;
      notifyListeners();
    }
  }

  void setRoleFilter(String? role) {
    roleFilter = role;
    notifyListeners();
  }
}
