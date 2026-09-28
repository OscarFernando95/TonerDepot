import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../config/app_config.dart';

const _tokenKey = 'toner_jwt_token';
const _baseUrlKey = 'toner_api_base_url';

/// Excepción con el mensaje ya listo para mostrarle al usuario — mismo patrón
/// que `err.response?.data?.title` en el frontend web (ver http.ts), porque
/// el backend responde ProblemDetails (título legible) tanto en errores de
/// negocio (400) como de validación.
class ApiException implements Exception {
  final String message;
  final int? statusCode;
  ApiException(this.message, {this.statusCode});

  @override
  String toString() => message;
}

/// Se invoca cuando el servidor responde 401 (token vencido/inválido) —
/// ApiClient no conoce el estado de auth, así que delega el logout a quien
/// lo registre (ver AuthState en main.dart).
typedef UnauthorizedCallback = void Function();

class ApiClient {
  ApiClient._internal(this._dio);

  static final ApiClient instance = ApiClient._internal(Dio());

  final Dio _dio;
  final _storage = const FlutterSecureStorage();
  UnauthorizedCallback? onUnauthorized;
  String? _token;

  Dio get dio => _dio;

  Future<void> init() async {
    final savedBaseUrl = await _storage.read(key: _baseUrlKey);
    _dio.options
      ..baseUrl = savedBaseUrl ?? AppConfig.defaultBaseUrl
      ..connectTimeout = const Duration(seconds: 15)
      ..receiveTimeout = const Duration(seconds: 15);

    _token = await _storage.read(key: _tokenKey);

    _dio.interceptors.clear();
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          // Informativo para la auditoría y el mensaje de sesión única (nunca una decisión de seguridad).
          options.headers['X-Client-Type'] = 'mobile';
          if (_token != null) {
            options.headers['Authorization'] = 'Bearer $_token';
          }
          handler.next(options);
        },
        onError: (error, handler) {
          if (error.response?.statusCode == 401) {
            onUnauthorized?.call();
          }
          handler.next(error);
        },
      ),
    );
  }

  Future<String> get baseUrl async => _dio.options.baseUrl;

  Future<void> setBaseUrl(String url) async {
    _dio.options.baseUrl = url;
    await _storage.write(key: _baseUrlKey, value: url);
  }

  Future<void> setToken(String? token) async {
    _token = token;
    if (token == null) {
      await _storage.delete(key: _tokenKey);
    } else {
      await _storage.write(key: _tokenKey, value: token);
    }
  }

  bool get hasToken => _token != null;

  /// Solo para el canal en tiempo real (SignalR no puede usar el interceptor de Dio): la API REST sigue
  /// yendo por el interceptor de arriba, nunca por acceso directo a este campo.
  String? get currentToken => _token;

  /// Traduce cualquier DioException a un mensaje presentable. El backend
  /// devuelve ProblemDetails con "title" (ver ExceptionHandlingMiddleware) —
  /// si además vienen errores de campo (FluentValidation), los concatena.
  static ApiException translate(Object error) {
    if (error is DioException) {
      final status = error.response?.statusCode;
      final data = error.response?.data;
      if (data is Map) {
        final title = data['title']?.toString();
        final errors = data['errors'];
        if (errors is Map && errors.isNotEmpty) {
          final detail = errors.values
              .expand((v) => v is List ? v : [v])
              .map((e) => e.toString())
              .join('\n');
          return ApiException(
            detail.isNotEmpty ? detail : (title ?? 'Error de validación.'),
            statusCode: status,
          );
        }
        if (title != null && title.isNotEmpty) {
          return ApiException(title, statusCode: status);
        }
      }
      switch (error.type) {
        case DioExceptionType.connectionTimeout:
        case DioExceptionType.sendTimeout:
        case DioExceptionType.receiveTimeout:
          return ApiException(
            'El servidor no respondió a tiempo. Verifica la URL configurada y tu conexión.',
          );
        case DioExceptionType.connectionError:
          return ApiException(
            'No se pudo conectar con el servidor. Verifica la URL configurada y que la API esté corriendo.',
          );
        default:
          return ApiException(
            'Ocurrió un error inesperado (${status ?? 's/n'}).',
            statusCode: status,
          );
      }
    }
    return ApiException(error.toString());
  }
}
