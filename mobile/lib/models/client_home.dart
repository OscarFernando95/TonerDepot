import 'maintenance_order.dart';

/// Resumen del inicio del Cliente (GET /client-portal/home). Solo servicio: el backend nunca manda contratos,
/// límites de alquiler ni nada interno. Todo el parseo tolera nulos y tipos inesperados, para que un campo que
/// falte no tumbe la pantalla.
class ClientHome {
  const ClientHome({
    this.clientName = '',
    this.locationCount = 0,
    this.openTickets = 0,
    this.ticketsInProgress = 0,
    this.openMaintenance = 0,
    this.maintenanceScheduled = 0,
    this.maintenanceInProgress = 0,
    this.maintenanceWithoutVisitDate = 0,
    this.pendingInstallations = 0,
    this.installationsInProgress = 0,
    this.installedAssets = 0,
    this.assetsInMaintenance = 0,
    this.assetsWithOpenTicket = 0,
    this.recentTickets = const [],
    this.upcomingMaintenance = const [],
    this.installations = const [],
  });

  final String clientName;
  final int locationCount;
  final int openTickets;
  final int ticketsInProgress;
  final int openMaintenance;
  final int maintenanceScheduled;
  final int maintenanceInProgress;
  final int maintenanceWithoutVisitDate;
  final int pendingInstallations;
  final int installationsInProgress;
  final int installedAssets;
  final int assetsInMaintenance;
  final int assetsWithOpenTicket;
  final List<ClientHomeTicket> recentTickets;
  final List<ClientHomeMaintenance> upcomingMaintenance;
  final List<ClientHomeInstallation> installations;

  factory ClientHome.fromJson(Map<String, dynamic> json) => ClientHome(
    clientName: _str(json['clientName']) ?? '',
    locationCount: _int(json['locationCount']),
    openTickets: _int(json['openTickets']),
    ticketsInProgress: _int(json['ticketsInProgress']),
    openMaintenance: _int(json['openMaintenance']),
    maintenanceScheduled: _int(json['maintenanceScheduled']),
    maintenanceInProgress: _int(json['maintenanceInProgress']),
    maintenanceWithoutVisitDate: _int(json['maintenanceWithoutVisitDate']),
    pendingInstallations: _int(json['pendingInstallations']),
    installationsInProgress: _int(json['installationsInProgress']),
    installedAssets: _int(json['installedAssets']),
    assetsInMaintenance: _int(json['assetsInMaintenance']),
    assetsWithOpenTicket: _int(json['assetsWithOpenTicket']),
    recentTickets: _list(json['recentTickets'], ClientHomeTicket.fromJson),
    upcomingMaintenance: _list(
      json['upcomingMaintenance'],
      ClientHomeMaintenance.fromJson,
    ),
    installations: _list(
      json['installations'],
      ClientHomeInstallation.fromJson,
    ),
  );
}

class ClientHomeTicket {
  const ClientHomeTicket({
    required this.id,
    required this.equipment,
    required this.locationName,
    required this.description,
    required this.status,
    required this.priority,
    this.technicianName,
    this.createdAt,
  });

  final String id;
  final String equipment;
  final String locationName;
  final String description;

  /// Abierto | SinAsignar | Asignado | EnProceso | Resuelto | Cerrado | Cancelado
  final String status;

  /// Baja | Media | Alta | Critica
  final String priority;
  final String? technicianName;
  final DateTime? createdAt;

  factory ClientHomeTicket.fromJson(Map<String, dynamic> json) =>
      ClientHomeTicket(
        id: _str(json['id']) ?? '',
        equipment: _str(json['equipment']) ?? '',
        locationName: _str(json['locationName']) ?? '',
        description: _str(json['description']) ?? '',
        status: _str(json['status']) ?? '',
        priority: _str(json['priority']) ?? 'Media',
        technicianName: _str(json['technicianName']),
        createdAt: MaintenanceOrder.parseInstant(json['createdAt']),
      );
}

class ClientHomeMaintenance {
  const ClientHomeMaintenance({
    required this.id,
    required this.equipment,
    required this.status,
    this.locationName,
    this.scheduledDate,
    this.plannedAt,
    this.technicianName,
    this.commentCount = 0,
  });

  final String id;
  final String equipment;
  final String? locationName;

  /// Pendiente | Asignada | EnProceso
  final String status;

  /// Fecha objetivo del cronograma (no es la visita).
  final DateTime? scheduledDate;

  /// La visita que fijó el técnico (instante UTC), si ya la programó.
  final DateTime? plannedAt;
  final String? technicianName;
  final int commentCount;

  bool get inProgress => status == 'EnProceso';

  factory ClientHomeMaintenance.fromJson(Map<String, dynamic> json) =>
      ClientHomeMaintenance(
        id: _str(json['id']) ?? '',
        equipment: _str(json['equipment']) ?? '',
        locationName: _str(json['locationName']),
        status: _str(json['status']) ?? '',
        scheduledDate: MaintenanceOrder.parseInstant(json['scheduledDate']),
        plannedAt: MaintenanceOrder.parseInstant(json['plannedAt']),
        technicianName: _str(json['technicianName']),
        commentCount: _int(json['commentCount']),
      );
}

class ClientHomeInstallation {
  const ClientHomeInstallation({
    required this.assetId,
    required this.equipment,
    this.locationName,
    this.inProgress = false,
  });

  final String assetId;
  final String equipment;
  final String? locationName;

  /// Un técnico la está instalando ahora.
  final bool inProgress;

  factory ClientHomeInstallation.fromJson(Map<String, dynamic> json) =>
      ClientHomeInstallation(
        assetId: _str(json['assetId']) ?? '',
        equipment: _str(json['equipment']) ?? '',
        locationName: _str(json['locationName']),
        inProgress: json['inProgress'] == true,
      );
}

String? _str(Object? raw) {
  if (raw is! String) return null;
  return raw.trim().isEmpty ? null : raw;
}

int _int(Object? raw) => raw is num ? raw.toInt() : 0;

/// Lista tolerante: ignora lo que no sea un objeto y los elementos que no se puedan interpretar.
List<T> _list<T>(Object? raw, T Function(Map<String, dynamic>) parse) {
  if (raw is! List) return const [];
  final result = <T>[];
  for (final item in raw) {
    if (item is! Map) continue;
    try {
      result.add(parse(Map<String, dynamic>.from(item)));
    } catch (_) {
      // Un elemento corrupto no debe esconder los demás.
    }
  }
  return result;
}
