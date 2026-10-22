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
  final bool includesGeneral;
  final bool includesUnits;
  final bool includesConsumables;

  /// Pedida a demanda por el staff (no por umbral del cronograma), con el motivo que dio.
  final bool isManual;

  /// La inició el propio técnico asignado (no el staff): es la única que él puede cancelar, y solo mientras siga Asignada.
  final bool isSelfStarted;
  final String? reason;
  final String scheduledDate;
  final String? completedAt;
  final String createdAt;

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
    required this.includesGeneral,
    required this.includesUnits,
    required this.includesConsumables,
    this.isManual = false,
    this.isSelfStarted = false,
    this.reason,
    required this.scheduledDate,
    required this.completedAt,
    required this.createdAt,
  });

  /// La web solo habilita asignar/completar/cancelar en estos dos estados
  /// (el backend es más permisivo con completar/cancelar, pero replicamos el
  /// gate de UI para paridad exacta — ver MaintenanceOrderDetailView.vue).
  bool get canManage => status == 'Pendiente' || status == 'Asignada';

  /// Todavía por hacer o en curso (ni completada ni cancelada).
  bool get isOpen => status != 'Completada' && status != 'Cancelada';

  /// Qué cubre la visita, en el orden en que se muestra al cliente. Vacío si el backend no marcó ninguna.
  List<String> get includesLabels => [
    if (includesGeneral) 'Mantenimiento general',
    if (includesUnits) 'Unidades',
    if (includesConsumables) 'Insumos',
  ];

  /// Portal Cliente: abiertas primero (la más próxima arriba) y luego las cerradas (la más reciente arriba).
  /// Las fechas llegan como ISO 8601, así que comparar el texto equivale a comparar cronológicamente.
  static List<MaintenanceOrder> sortForClient(
    Iterable<MaintenanceOrder> orders,
  ) {
    final open = orders.where((o) => o.isOpen).toList()
      ..sort((a, b) => a.scheduledDate.compareTo(b.scheduledDate));
    final closed = orders.where((o) => !o.isOpen).toList()
      ..sort((a, b) => b.scheduledDate.compareTo(a.scheduledDate));
    return [...open, ...closed];
  }

  /// "2026-10-22T00:00:00" -> "22/10/2026". Si no se puede interpretar, devuelve el texto tal cual.
  static String formatScheduledDate(String iso) {
    final datePart = iso.split('T').first;
    final parts = datePart.split('-');
    if (parts.length != 3) return iso;
    return '${parts[2]}/${parts[1]}/${parts[0]}';
  }

  /// El técnico puede arrepentirse antes del check-in (POST /technicians/me/maintenance-orders/{id}/cancel).
  bool get canCancelByTechnician => isSelfStarted && status == 'Asignada';

  factory MaintenanceOrder.fromJson(Map<String, dynamic> json) =>
      MaintenanceOrder(
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
        includesGeneral: json['includesGeneral'] as bool? ?? false,
        includesUnits: json['includesUnits'] as bool? ?? false,
        includesConsumables: json['includesConsumables'] as bool? ?? false,
        isManual: json['isManual'] as bool? ?? false,
        isSelfStarted: json['isSelfStarted'] as bool? ?? false,
        reason: json['reason'] as String?,
        scheduledDate: json['scheduledDate'] as String,
        completedAt: json['completedAt'] as String?,
        createdAt: json['createdAt'] as String,
      );
}
