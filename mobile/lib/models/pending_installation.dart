/// Espejo de PendingInstallationDto (frontend-web/src/api/technicianSelf.ts)
/// — un activo vinculado a un contrato que todavía no se instaló en la sede
/// del cliente.
class PendingInstallation {
  final String assetId;
  final String assetBrandName;
  final String model;
  final String serialNumber;
  final String clientId;
  final String clientName;
  final String clientLocationId;
  final String clientLocationName;
  final String? cityName;
  final String? contractId;
  // Vigencia del contrato activo de este activo — null si no tiene contrato vinculado. Solo sirve
  // para restringir el selector de fecha en el check-out (comodidad de UI); la validación real vive
  // en el backend (TechnicianCheckInService.EnsureCounterDateWithinContractAsync).
  final DateTime? contractStartDate;
  final DateTime? contractEndDate;
  final bool takenByAnotherTechnician;

  /// false si ahora no puede iniciarla por horario (fuera de jornada, festivo o permiso). El backend lo exige igual.
  final bool canStartNow;

  PendingInstallation({
    required this.assetId,
    required this.assetBrandName,
    required this.model,
    required this.serialNumber,
    required this.clientId,
    required this.clientName,
    required this.clientLocationId,
    required this.clientLocationName,
    required this.cityName,
    required this.contractId,
    required this.contractStartDate,
    required this.contractEndDate,
    required this.takenByAnotherTechnician,
    this.canStartNow = true,
  });

  factory PendingInstallation.fromJson(Map<String, dynamic> json) =>
      PendingInstallation(
        assetId: json['assetId'] as String,
        assetBrandName: json['assetBrandName'] as String? ?? '',
        model: json['model'] as String? ?? '',
        serialNumber: json['serialNumber'] as String? ?? '',
        clientId: json['clientId'] as String,
        clientName: json['clientName'] as String? ?? '',
        clientLocationId: json['clientLocationId'] as String,
        clientLocationName: json['clientLocationName'] as String? ?? '',
        cityName: json['cityName'] as String?,
        contractId: json['contractId'] as String?,
        contractStartDate: json['contractStartDate'] != null
            ? DateTime.parse(json['contractStartDate'] as String)
            : null,
        contractEndDate: json['contractEndDate'] != null
            ? DateTime.parse(json['contractEndDate'] as String)
            : null,
        takenByAnotherTechnician:
            json['takenByAnotherTechnician'] as bool? ?? false,
        canStartNow: json['canStartNow'] as bool? ?? true,
      );
}
