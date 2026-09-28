class CurrentUser {
  final String id;
  final String cedula;
  final String? email;
  final String fullName;
  final String role;
  final String? clientId;
  final String? technicianId;
  final bool mustChangePassword;

  CurrentUser({
    required this.id,
    required this.cedula,
    required this.email,
    required this.fullName,
    required this.role,
    required this.clientId,
    required this.technicianId,
    required this.mustChangePassword,
  });

  factory CurrentUser.fromJson(Map<String, dynamic> json) => CurrentUser(
    id: json['id'] as String,
    cedula: json['cedula'] as String,
    email: json['email'] as String?,
    fullName: json['fullName'] as String,
    role: json['role'] as String,
    clientId: json['clientId'] as String?,
    technicianId: json['technicianId'] as String?,
    mustChangePassword: json['mustChangePassword'] as bool? ?? false,
  );

  CurrentUser copyWith({bool? mustChangePassword}) => CurrentUser(
    id: id,
    cedula: cedula,
    email: email,
    fullName: fullName,
    role: role,
    clientId: clientId,
    technicianId: technicianId,
    mustChangePassword: mustChangePassword ?? this.mustChangePassword,
  );
}

class LoginResult {
  final bool succeeded;
  final String? token;
  final String? expiresAtUtc;
  final CurrentUser? user;

  LoginResult({
    required this.succeeded,
    required this.token,
    required this.expiresAtUtc,
    required this.user,
  });

  factory LoginResult.fromJson(Map<String, dynamic> json) => LoginResult(
    succeeded: json['succeeded'] as bool? ?? false,
    token: json['token'] as String?,
    expiresAtUtc: json['expiresAtUtc'] as String?,
    user: json['user'] != null
        ? CurrentUser.fromJson(json['user'] as Map<String, dynamic>)
        : null,
  );
}
