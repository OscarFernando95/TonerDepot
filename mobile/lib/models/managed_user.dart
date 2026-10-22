/// Espejo de UserDto (gestión, backend/src/Toner.Application/Users/Dtos/UserDto.cs)
/// — NO es el mismo shape que CurrentUser (el de login/`/auth/me`): el campo
/// de rol se llama `roleName` acá (no `role`), y trae phone/address/cityId/
/// isActive/createdAt que CurrentUser no tiene.
class ManagedUser {
  final String id;
  final String cedula;
  final String? email;
  final String fullName;
  final String? phone;
  final String? address;
  final String? cityId;
  final String? cityName;
  final String roleName;
  final bool isActive;
  final bool mustChangePassword;
  final String? clientId;
  final String? technicianId;
  final String createdAt;

  /// Solo aplica a usuarios Cliente: true si el administrador limitó el acceso
  /// a algunas sedes de su empresa. `allowedLocationIds` va vacío si no hay
  /// restricción (= todas las sedes).
  final bool restrictedToLocations;
  final List<String> allowedLocationIds;

  ManagedUser({
    required this.id,
    required this.cedula,
    required this.email,
    required this.fullName,
    required this.phone,
    required this.address,
    required this.cityId,
    required this.cityName,
    required this.roleName,
    required this.isActive,
    required this.mustChangePassword,
    required this.clientId,
    required this.technicianId,
    required this.createdAt,
    this.restrictedToLocations = false,
    this.allowedLocationIds = const [],
  });

  factory ManagedUser.fromJson(Map<String, dynamic> json) => ManagedUser(
    id: json['id'] as String,
    cedula: json['cedula'] as String,
    email: json['email'] as String?,
    fullName: json['fullName'] as String,
    phone: json['phone'] as String?,
    address: json['address'] as String?,
    cityId: json['cityId'] as String?,
    cityName: json['cityName'] as String?,
    roleName: json['roleName'] as String,
    isActive: json['isActive'] as bool? ?? true,
    mustChangePassword: json['mustChangePassword'] as bool? ?? false,
    clientId: json['clientId'] as String?,
    technicianId: json['technicianId'] as String?,
    createdAt: json['createdAt'] as String,
    restrictedToLocations: json['restrictedToLocations'] as bool? ?? false,
    allowedLocationIds:
        (json['allowedLocationIds'] as List<dynamic>?)
            ?.whereType<String>()
            .toList() ??
        const [],
  );
}

/// Respuesta de crear usuario y de resetear contraseña — la única vez que la
/// contraseña en claro sale de la API. Mostrarla una vez en pantalla, nunca
/// persistirla en el cliente.
class UserWithGeneratedPassword {
  final ManagedUser user;
  final String generatedPassword;

  UserWithGeneratedPassword({
    required this.user,
    required this.generatedPassword,
  });

  factory UserWithGeneratedPassword.fromJson(Map<String, dynamic> json) =>
      UserWithGeneratedPassword(
        user: ManagedUser.fromJson(json),
        generatedPassword: json['generatedPassword'] as String,
      );
}
