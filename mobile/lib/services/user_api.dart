import 'package:flutter/foundation.dart';

import '../models/managed_user.dart';
import '../models/paged_result.dart';
import 'api_client.dart';

/// Espejo de frontend-web/src/api/users.ts. Todo el controller es
/// RoleNames.Administrador únicamente — ni Coordinador entra acá, a
/// diferencia de los demás módulos de la Fase F.
class UserApi {
  UserApi(this._client);
  final ApiClient _client;

  Future<PagedResult<ManagedUser>> list({
    required int page,
    int pageSize = 50,
  }) async {
    try {
      final response = await _client.dio.get(
        '/users',
        queryParameters: {'page': page, 'pageSize': pageSize},
      );
      return PagedResult<ManagedUser>.fromJson(
        response.data as Map<String, dynamic>,
        ManagedUser.fromJson,
      );
    } catch (e, st) {
      debugPrint('UserApi.list failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// El backend genera la contraseña y la manda por correo cifrado — nunca
  /// se acepta una escrita a mano. Si `roleName == 'Tecnico'`, crea también
  /// el registro de Technician en el mismo paso (no hay que llamar aparte a
  /// technician_management_api). `clientId` obligatorio solo si roleName es
  /// 'Cliente'.
  Future<UserWithGeneratedPassword> create({
    required String cedula,
    String? email,
    required String fullName,
    required String phone,
    required String address,
    required String cityId,
    required String roleName,
    String? clientId,
  }) async {
    try {
      final response = await _client.dio.post(
        '/users',
        data: {
          'cedula': cedula,
          'email': ?email,
          'fullName': fullName,
          'phone': phone,
          'address': address,
          'cityId': cityId,
          'roleName': roleName,
          'clientId': ?clientId,
        },
      );
      return UserWithGeneratedPassword.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('UserApi.create failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Solo datos de perfil — el backend no permite cambiar rol/clientId/
  /// technicianId desde acá (reasignar rol tiene efectos en cascada que no
  /// están implementados; no hay endpoint para eso).
  Future<ManagedUser> update(
    String id, {
    required String cedula,
    String? email,
    required String fullName,
    required String phone,
    required String address,
    required String cityId,
  }) async {
    try {
      final response = await _client.dio.patch(
        '/users/$id',
        data: {
          'cedula': cedula,
          'email': ?email,
          'fullName': fullName,
          'phone': phone,
          'address': address,
          'cityId': cityId,
        },
      );
      return ManagedUser.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('UserApi.update failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Cierra todas las sesiones vivas del usuario (Administrador). Devuelve cuántas cerró.
  Future<int> revokeSessions(String id) async {
    try {
      final response = await _client.dio.post('/users/$id/sessions/revoke');
      return (response.data as Map<String, dynamic>)['revoked'] as int;
    } catch (e, st) {
      debugPrint('UserApi.revokeSessions failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  Future<ManagedUser> setStatus(String id, bool isActive) async {
    try {
      final response = await _client.dio.patch(
        '/users/$id/status',
        data: {'isActive': isActive},
      );
      return ManagedUser.fromJson(response.data as Map<String, dynamic>);
    } catch (e, st) {
      debugPrint('UserApi.setStatus failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }

  /// Body vacío — nunca se manda una contraseña nueva desde el cliente.
  /// Invalida el SecurityStamp (cierra sesiones activas de ese usuario).
  Future<UserWithGeneratedPassword> resetPassword(String id) async {
    try {
      final response = await _client.dio.post('/users/$id/reset-password');
      return UserWithGeneratedPassword.fromJson(
        response.data as Map<String, dynamic>,
      );
    } catch (e, st) {
      debugPrint('UserApi.resetPassword failed: $e\n$st');
      throw ApiClient.translate(e);
    }
  }
}
