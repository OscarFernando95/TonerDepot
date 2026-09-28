/// Espejo de DashboardSummaryDto (backend/src/Toner.Application/Dashboard/Dtos/DashboardSummaryDto.cs)
/// — un solo endpoint compuesto arma las 5 tarjetas del dashboard, ver
/// DashboardController.cs. Los DTOs internos viven en este mismo archivo,
/// igual que en el backend, porque solo los usa esta pantalla.
class DashboardSummary {
  final int periodDays;
  final Mttr mttr;
  final MaintenanceCompliance maintenanceCompliance;
  final List<CityTicketBacklog> ticketsByCity;
  final List<TechnicianUtilization> technicianUtilization;
  final SlaCompliance slaCompliance;

  DashboardSummary({
    required this.periodDays,
    required this.mttr,
    required this.maintenanceCompliance,
    required this.ticketsByCity,
    required this.technicianUtilization,
    required this.slaCompliance,
  });

  factory DashboardSummary.fromJson(Map<String, dynamic> json) =>
      DashboardSummary(
        periodDays: json['periodDays'] as int,
        mttr: Mttr.fromJson(json['mttr'] as Map<String, dynamic>),
        maintenanceCompliance: MaintenanceCompliance.fromJson(
          json['maintenanceCompliance'] as Map<String, dynamic>,
        ),
        ticketsByCity: (json['ticketsByCity'] as List<dynamic>? ?? [])
            .map((e) => CityTicketBacklog.fromJson(e as Map<String, dynamic>))
            .toList(),
        technicianUtilization:
            (json['technicianUtilization'] as List<dynamic>? ?? [])
                .map(
                  (e) =>
                      TechnicianUtilization.fromJson(e as Map<String, dynamic>),
                )
                .toList(),
        slaCompliance: SlaCompliance.fromJson(
          json['slaCompliance'] as Map<String, dynamic>,
        ),
      );
}

class Mttr {
  final double? averageResolutionHours;
  final int resolvedTicketCount;

  Mttr({
    required this.averageResolutionHours,
    required this.resolvedTicketCount,
  });

  factory Mttr.fromJson(Map<String, dynamic> json) => Mttr(
    averageResolutionHours: (json['averageResolutionHours'] as num?)
        ?.toDouble(),
    resolvedTicketCount: json['resolvedTicketCount'] as int? ?? 0,
  );
}

class MaintenanceCompliance {
  final int windowDays;
  final double? onTimePercentage;
  final int completedCount;
  final int onTimeCount;

  MaintenanceCompliance({
    required this.windowDays,
    required this.onTimePercentage,
    required this.completedCount,
    required this.onTimeCount,
  });

  factory MaintenanceCompliance.fromJson(Map<String, dynamic> json) =>
      MaintenanceCompliance(
        windowDays: json['windowDays'] as int? ?? 0,
        onTimePercentage: (json['onTimePercentage'] as num?)?.toDouble(),
        completedCount: json['completedCount'] as int? ?? 0,
        onTimeCount: json['onTimeCount'] as int? ?? 0,
      );
}

class CityTicketBacklog {
  final String cityId;
  final String cityName;
  final int openCount;
  final int unassignedCount;

  CityTicketBacklog({
    required this.cityId,
    required this.cityName,
    required this.openCount,
    required this.unassignedCount,
  });

  factory CityTicketBacklog.fromJson(Map<String, dynamic> json) =>
      CityTicketBacklog(
        cityId: json['cityId'] as String,
        cityName: json['cityName'] as String,
        openCount: json['openCount'] as int? ?? 0,
        unassignedCount: json['unassignedCount'] as int? ?? 0,
      );
}

class TechnicianUtilization {
  final String technicianId;
  final String technicianName;
  final double hoursLogged;
  final double utilizationPercentage;

  TechnicianUtilization({
    required this.technicianId,
    required this.technicianName,
    required this.hoursLogged,
    required this.utilizationPercentage,
  });

  factory TechnicianUtilization.fromJson(Map<String, dynamic> json) =>
      TechnicianUtilization(
        technicianId: json['technicianId'] as String,
        technicianName: json['technicianName'] as String,
        hoursLogged: (json['hoursLogged'] as num?)?.toDouble() ?? 0,
        utilizationPercentage:
            (json['utilizationPercentage'] as num?)?.toDouble() ?? 0,
      );
}

class SlaCompliance {
  final double? overallCompliancePercentage;
  final List<SlaPriorityCompliance> byPriority;

  SlaCompliance({
    required this.overallCompliancePercentage,
    required this.byPriority,
  });

  factory SlaCompliance.fromJson(Map<String, dynamic> json) => SlaCompliance(
    overallCompliancePercentage: (json['overallCompliancePercentage'] as num?)
        ?.toDouble(),
    byPriority: (json['byPriority'] as List<dynamic>? ?? [])
        .map((e) => SlaPriorityCompliance.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

class SlaPriorityCompliance {
  final String priority;
  final int targetHours;
  final int resolvedCount;
  final int withinSlaCount;
  final double? compliancePercentage;

  SlaPriorityCompliance({
    required this.priority,
    required this.targetHours,
    required this.resolvedCount,
    required this.withinSlaCount,
    required this.compliancePercentage,
  });

  factory SlaPriorityCompliance.fromJson(Map<String, dynamic> json) =>
      SlaPriorityCompliance(
        priority: json['priority'] as String,
        targetHours: json['targetHours'] as int? ?? 0,
        resolvedCount: json['resolvedCount'] as int? ?? 0,
        withinSlaCount: json['withinSlaCount'] as int? ?? 0,
        compliancePercentage: (json['compliancePercentage'] as num?)
            ?.toDouble(),
      );
}
