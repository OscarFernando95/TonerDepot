/// Visita de un técnico (TimeLog) con la ubicación registrada al llegar y al cerrar, comparada con la sede.
class TechnicianVisit {
  TechnicianVisit({
    required this.id,
    required this.kind,
    required this.startTime,
    required this.endTime,
    required this.checkInStatus,
    required this.checkInDistanceMeters,
    required this.checkOutStatus,
    required this.checkOutDistanceMeters,
  });

  final String id;
  final String kind; // Ticket | Mantenimiento | Instalación
  final DateTime startTime;
  final DateTime? endTime;
  /// EnSitio | FueraDeSitio | SinUbicacion | SedeSinCoordenadas
  final String? checkInStatus;
  final double? checkInDistanceMeters;
  final String? checkOutStatus;
  final double? checkOutDistanceMeters;

  factory TechnicianVisit.fromJson(Map<String, dynamic> json) => TechnicianVisit(
    id: json['id'] as String,
    kind: json['serviceTicketId'] != null
        ? 'Ticket'
        : json['maintenanceOrderId'] != null
        ? 'Mantenimiento'
        : 'Instalación',
    startTime: DateTime.parse(json['startTime'] as String).toLocal(),
    endTime: json['endTime'] == null ? null : DateTime.parse(json['endTime'] as String).toLocal(),
    checkInStatus: json['checkInLocationStatus'] as String?,
    checkInDistanceMeters: (json['checkInDistanceMeters'] as num?)?.toDouble(),
    checkOutStatus: json['checkOutLocationStatus'] as String?,
    checkOutDistanceMeters: (json['checkOutDistanceMeters'] as num?)?.toDouble(),
  );
}

/// "En sitio" / "Fuera de sitio (2,3 km)" / "Sin ubicación" / "Sede sin coordenadas".
String describeLocation(String? status, double? distanceMeters) {
  switch (status) {
    case 'EnSitio':
      return 'En sitio';
    case 'FueraDeSitio':
      if (distanceMeters == null) return 'Fuera de sitio';
      return distanceMeters >= 1000
          ? 'Fuera de sitio (${(distanceMeters / 1000).toStringAsFixed(1)} km)'
          : 'Fuera de sitio (${distanceMeters.round()} m)';
    case 'SinUbicacion':
      return 'Sin ubicación';
    case 'SedeSinCoordenadas':
      return 'Sede sin coordenadas';
    default:
      return '—';
  }
}
