class MaintenanceOrder {
  final String id;
  final String maintenanceScheduleId;
  final String assetId;
  final String assetBrandName;
  final String assetModel;
  final String assetSerialNumber;
  final String status; // Pendiente|Asignada|EnProceso|Completada|Cancelada
  final String? technicianId;
  final String? technicianName;
  final String? clientLocationName;
  final String? cityName;
  final String scheduledDate;
  final String? completedAt;

  MaintenanceOrder({
    required this.id,
    required this.maintenanceScheduleId,
    required this.assetId,
    required this.assetBrandName,
    required this.assetModel,
    required this.assetSerialNumber,
    required this.status,
    required this.technicianId,
    required this.technicianName,
    required this.clientLocationName,
    required this.cityName,
    required this.scheduledDate,
    required this.completedAt,
  });

  factory MaintenanceOrder.fromJson(Map<String, dynamic> json) => MaintenanceOrder(
        id: json['id'] as String,
        maintenanceScheduleId: json['maintenanceScheduleId'] as String,
        assetId: json['assetId'] as String,
        assetBrandName: json['assetBrandName'] as String,
        assetModel: json['assetModel'] as String,
        assetSerialNumber: json['assetSerialNumber'] as String,
        status: json['status'] as String,
        technicianId: json['technicianId'] as String?,
        technicianName: json['technicianName'] as String?,
        clientLocationName: json['clientLocationName'] as String?,
        cityName: json['cityName'] as String?,
        scheduledDate: json['scheduledDate'] as String,
        completedAt: json['completedAt'] as String?,
      );
}
