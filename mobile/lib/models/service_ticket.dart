class ServiceTicket {
  final String id;
  final String clientLocationId;
  final String clientLocationName;
  final String clientId;
  final String clientName;
  final String? cityName;
  final String? assetId;
  final String? assetBrandName;
  final String? assetModel;
  final String? assetSerialNumber;
  final String? externalAssetBrand;
  final String? externalAssetModel;
  final num? externalAssetCounter;
  final String reportedByUserId;
  final String reportedByUserName;
  final String description;
  final String
  status; // Abierto|SinAsignar|Asignado|EnProceso|Resuelto|Cerrado|Cancelado
  final String priority; // Baja|Media|Alta|Critica
  final String? technicianId;
  final String? technicianName;
  final String? resolvedAt;
  final String? closedAt;
  final String createdAt;

  /// Solo vienen en el detalle y solo para staff/técnico (ver ServiceTicketDto.cs).
  final String? resolutionNotes;
  final int? resolutionDurationMinutes;

  ServiceTicket({
    required this.id,
    required this.clientLocationId,
    required this.clientLocationName,
    required this.clientId,
    required this.clientName,
    required this.cityName,
    required this.assetId,
    required this.assetBrandName,
    required this.assetModel,
    required this.assetSerialNumber,
    required this.externalAssetBrand,
    required this.externalAssetModel,
    required this.externalAssetCounter,
    required this.reportedByUserId,
    required this.reportedByUserName,
    required this.description,
    required this.status,
    required this.priority,
    required this.technicianId,
    required this.technicianName,
    required this.resolvedAt,
    required this.closedAt,
    required this.createdAt,
    this.resolutionNotes,
    this.resolutionDurationMinutes,
  });

  /// Ticket sobre un equipo de un cliente externo, no catalogado en el inventario
  /// (ver comentario del mismo nombre en frontend-web/src/api/types.ts).
  bool get isExternal => assetId == null;

  /// El equipo externo puede no tener info todavía (el técnico la captura recién
  /// en el checkout) — tres estados posibles, no un booleano: catalogado, externo
  /// ya documentado, o externo aún sin datos. Espejo de TicketDetailView.vue.
  bool get hasExternalAssetInfo =>
      externalAssetBrand != null ||
      externalAssetModel != null ||
      externalAssetCounter != null;

  factory ServiceTicket.fromJson(Map<String, dynamic> json) => ServiceTicket(
    id: json['id'] as String,
    clientLocationId: json['clientLocationId'] as String,
    clientLocationName: json['clientLocationName'] as String,
    clientId: json['clientId'] as String,
    clientName: json['clientName'] as String,
    cityName: json['cityName'] as String?,
    assetId: json['assetId'] as String?,
    assetBrandName: json['assetBrandName'] as String?,
    assetModel: json['assetModel'] as String?,
    assetSerialNumber: json['assetSerialNumber'] as String?,
    externalAssetBrand: json['externalAssetBrand'] as String?,
    externalAssetModel: json['externalAssetModel'] as String?,
    externalAssetCounter: json['externalAssetCounter'] as num?,
    reportedByUserId: json['reportedByUserId'] as String,
    reportedByUserName: json['reportedByUserName'] as String,
    description: json['description'] as String,
    status: json['status'] as String,
    priority: json['priority'] as String,
    technicianId: json['technicianId'] as String?,
    technicianName: json['technicianName'] as String?,
    resolvedAt: json['resolvedAt'] as String?,
    closedAt: json['closedAt'] as String?,
    createdAt: json['createdAt'] as String,
    resolutionNotes: json['resolutionNotes'] as String?,
    resolutionDurationMinutes: (json['resolutionDurationMinutes'] as num?)
        ?.toInt(),
  );
}
