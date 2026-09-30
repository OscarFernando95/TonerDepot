/// Espejo de AssignmentHistoryDto (frontend-web/src/api/types.ts) — DTO
/// compartido por /tickets/{id}/assignment-history y
/// /maintenance-orders/{id}/assignment-history.
class AssignmentHistory {
  final String id;
  final String? technicianId;
  final String? technicianName;
  final String? assignedByUserName;
  final String assignmentType; // Manual | Automatico
  final String? reason;
  final String assignedAt;

  AssignmentHistory({
    required this.id,
    required this.technicianId,
    required this.technicianName,
    required this.assignedByUserName,
    required this.assignmentType,
    required this.reason,
    required this.assignedAt,
  });

  factory AssignmentHistory.fromJson(Map<String, dynamic> json) =>
      AssignmentHistory(
        id: json['id'] as String,
        technicianId: json['technicianId'] as String?,
        technicianName: json['technicianName'] as String?,
        assignedByUserName: json['assignedByUserName'] as String?,
        assignmentType: json['assignmentType'] as String,
        reason: json['reason'] as String?,
        assignedAt: json['assignedAt'] as String,
      );
}
