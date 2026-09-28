/// Cronograma de mantenimiento de un activo — espejo de MaintenanceScheduleDto.
/// La urgencia se calcula igual que en MaintenanceSchedulesView.vue: el
/// disparo real es "lo primero que ocurra" (días O impresiones), así que
/// "cerca" basta en UNA medida pero "lejos" exige estar lejos en AMBAS.
class MaintenanceSchedule {
  final String id;
  final String assetId;
  final String assetBrandName;
  final String assetModel;
  final String assetSerialNumber;
  final String contractId;
  final String clientId;
  final String clientName;
  final String? clientLocationName;
  final String? cityName;
  final String? area;
  final bool isActive;
  final int? lastKnownCounter;
  final String? lastMaintenanceAt;
  final List<String> lastMaintenanceCodes;
  final String? nextMaintenanceAt;
  final int nextMaintenanceCounter;
  final List<String> nextMaintenanceCodes;

  MaintenanceSchedule({
    required this.id,
    required this.assetId,
    required this.assetBrandName,
    required this.assetModel,
    required this.assetSerialNumber,
    required this.contractId,
    required this.clientId,
    required this.clientName,
    required this.clientLocationName,
    required this.cityName,
    required this.area,
    required this.isActive,
    required this.lastKnownCounter,
    required this.lastMaintenanceAt,
    required this.lastMaintenanceCodes,
    required this.nextMaintenanceAt,
    required this.nextMaintenanceCounter,
    required this.nextMaintenanceCodes,
  });

  int? get printsRemaining => lastKnownCounter == null
      ? null
      : nextMaintenanceCounter - lastKnownCounter!;

  int? get daysRemaining {
    final next = nextMaintenanceAt == null
        ? null
        : DateTime.tryParse(nextMaintenanceAt!);
    if (next == null) return null;
    return (next.difference(DateTime.now()).inMinutes / (60 * 24)).ceil();
  }

  /// far | soon | urgent | overdue
  String get urgency {
    final days = daysRemaining;
    final prints = printsRemaining;
    if ((days != null && days <= 0) || (prints != null && prints <= 0)) {
      return 'overdue';
    }
    if ((days != null && days <= 15) || (prints != null && prints <= 5000)) {
      return 'urgent';
    }
    if ((days != null && days <= 30) || (prints != null && prints <= 15000)) {
      return 'soon';
    }
    return 'far';
  }

  factory MaintenanceSchedule.fromJson(
    Map<String, dynamic> json,
  ) => MaintenanceSchedule(
    id: json['id'] as String,
    assetId: json['assetId'] as String,
    assetBrandName: json['assetBrandName'] as String,
    assetModel: json['assetModel'] as String,
    assetSerialNumber: json['assetSerialNumber'] as String,
    contractId: json['contractId'] as String,
    clientId: json['clientId'] as String,
    clientName: json['clientName'] as String,
    clientLocationName: json['clientLocationName'] as String?,
    cityName: json['cityName'] as String?,
    area: json['area'] as String?,
    isActive: json['isActive'] as bool,
    lastKnownCounter: (json['lastKnownCounter'] as num?)?.toInt(),
    lastMaintenanceAt: json['lastMaintenanceAt'] as String?,
    lastMaintenanceCodes: (json['lastMaintenanceCodes'] as List<dynamic>? ?? [])
        .cast<String>(),
    nextMaintenanceAt: json['nextMaintenanceAt'] as String?,
    nextMaintenanceCounter: (json['nextMaintenanceCounter'] as num).toInt(),
    nextMaintenanceCodes: (json['nextMaintenanceCodes'] as List<dynamic>? ?? [])
        .cast<String>(),
  );
}
