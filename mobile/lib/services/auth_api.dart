import '../models/current_user.dart';
import 'api_client.dart';

class AuthApi {
  AuthApi(this._client);
  final ApiClient _client;

  Future<LoginResult> login(String cedula, String password) async {
    try {
      final response = await _client.dio.post('/auth/login', data: {
        'cedula': cedula,
        'password': password,
      });
      return LoginResult.fromJson(response.data as Map<String, dynamic>);
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }

  Future<CurrentUser> me() async {
    try {
      final response = await _client.dio.get('/auth/me');
      return CurrentUser.fromJson(response.data as Map<String, dynamic>);
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }

  Future<void> changePassword(String currentPassword, String newPassword) async {
    try {
      await _client.dio.post('/auth/change-password', data: {
        'currentPassword': currentPassword,
        'newPassword': newPassword,
      });
    } catch (e) {
      throw ApiClient.translate(e);
    }
  }
}
