/// Espejo de TechnicianDto (gestión, no confundir con el self-service de
/// technician_api.dart) — backend/src/Toner.Api/Controllers/TechniciansController.cs.
class Technician {
  final String id;
  final String fullName;
  final String? phone;
  final String status; // Disponible|Ocupado|EnTransito|Inactivo
  final bool isActive;

  /// Dentro de su horario laboral ahora mismo (día hábil, tramo vigente, sin permiso).
  final bool isWorkingNow;

  /// Si está fuera de la oficina ahora, hasta cuándo.
  final DateTime? timeOffUntil;
  /// Zonas asignadas (normalmente una).
  final List<String> zoneNames;

  /// Municipios que cubre a través de sus zonas.
  final List<String> coverageCityNames;

  Technician({
    required this.id,
    required this.fullName,
    required this.phone,
    required this.status,
    required this.isActive,
    required this.isWorkingNow,
    required this.timeOffUntil,
    this.zoneNames = const [],
    required this.coverageCityNames,
  });

  factory Technician.fromJson(Map<String, dynamic> json) => Technician(
    id: json['id'] as String,
    fullName: json['fullName'] as String,
    phone: json['phone'] as String?,
    status: json['status'] as String,
    isActive: json['isActive'] as bool? ?? true,
    isWorkingNow: json['isWorkingNow'] as bool? ?? false,
    timeOffUntil: json['timeOffUntil'] == null
        ? null
        : DateTime.parse(json['timeOffUntil'] as String).toLocal(),
    zoneNames: (json['zoneNames'] as List<dynamic>? ?? []).cast<String>(),
    coverageCityNames: (json['coverageCityNames'] as List<dynamic>? ?? [])
        .cast<String>(),
  );
}
