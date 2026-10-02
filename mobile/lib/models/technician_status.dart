class TechnicianSelfStatus {
  final String technicianId;
  final String status; // Disponible | Ocupado | EnTransito | Inactivo
  final String? activeServiceTicketId;
  final String? activeMaintenanceOrderId;
  final String? activeAssetInstallationId;
  final String? checkedInAt;

  /// Avisos de stock insuficiente al cerrar la visita (la visita se cierra igual).
  final List<String> stockWarnings;

  TechnicianSelfStatus({
    required this.technicianId,
    required this.status,
    required this.activeServiceTicketId,
    required this.activeMaintenanceOrderId,
    required this.activeAssetInstallationId,
    required this.checkedInAt,
    this.stockWarnings = const [],
  });

  bool get isBusy => status == 'Ocupado';

  factory TechnicianSelfStatus.fromJson(Map<String, dynamic> json) =>
      TechnicianSelfStatus(
        technicianId: json['technicianId'] as String,
        status: json['status'] as String,
        activeServiceTicketId: json['activeServiceTicketId'] as String?,
        activeMaintenanceOrderId: json['activeMaintenanceOrderId'] as String?,
        activeAssetInstallationId: json['activeAssetInstallationId'] as String?,
        checkedInAt: json['checkedInAt'] as String?,
        stockWarnings: (json['stockWarnings'] as List<dynamic>? ?? [])
            .map((e) => e.toString())
            .toList(),
      );
}
