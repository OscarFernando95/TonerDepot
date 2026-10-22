import 'maintenance_order.dart';

/// Una entrada del hilo compartido de una orden de mantenimiento (cliente, técnico y staff). Puede ser un
/// comentario libre o el aviso de que el técnico (re)programó la fecha.
class MaintenanceOrderComment {
  const MaintenanceOrderComment({
    required this.id,
    required this.maintenanceOrderId,
    required this.kind,
    required this.body,
    required this.plannedAt,
    required this.authorUserId,
    required this.authorName,
    required this.authorRole,
    required this.createdAt,
  });

  static const kindComment = 'Comentario';
  static const kindSchedule = 'Programacion';

  final String id;
  final String maintenanceOrderId;

  /// 'Comentario' | 'Programacion'.
  final String kind;
  final String body;

  /// Solo en las entradas 'Programacion': la fecha (UTC) que quedó programada.
  final DateTime? plannedAt;
  final String authorUserId;
  final String authorName;

  /// 'Cliente' | 'Tecnico' | 'Administrador' | 'Coordinador'.
  final String authorRole;
  final DateTime createdAt;

  bool get isSchedule => kind == kindSchedule;
  bool get isFromClient => authorRole == 'Cliente';

  factory MaintenanceOrderComment.fromJson(Map<String, dynamic> json) =>
      MaintenanceOrderComment(
        id: json['id'] as String,
        maintenanceOrderId: json['maintenanceOrderId'] as String? ?? '',
        kind: json['kind'] as String? ?? kindComment,
        body: json['body'] as String? ?? '',
        plannedAt: MaintenanceOrder.parseInstant(json['plannedAt']),
        authorUserId: json['authorUserId'] as String? ?? '',
        authorName: json['authorName'] as String? ?? '',
        authorRole: json['authorRole'] as String? ?? '',
        createdAt:
            MaintenanceOrder.parseInstant(json['createdAt']) ??
            DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      );
}
