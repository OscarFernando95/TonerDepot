class AssetStatusLog {
  final String id;
  final String previousStatus;
  final String newStatus;
  final String changedAt;
  final String? changedByUserName;
  final String? notes;

  AssetStatusLog({
    required this.id,
    required this.previousStatus,
    required this.newStatus,
    required this.changedAt,
    required this.changedByUserName,
    required this.notes,
  });

  factory AssetStatusLog.fromJson(Map<String, dynamic> json) => AssetStatusLog(
        id: json['id'] as String,
        previousStatus: json['previousStatus'] as String,
        newStatus: json['newStatus'] as String,
        changedAt: json['changedAt'] as String,
        changedByUserName: json['changedByUserName'] as String?,
        notes: json['notes'] as String?,
      );
}
