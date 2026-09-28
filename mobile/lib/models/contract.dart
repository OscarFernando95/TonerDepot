/// Espejo de ContractDto (backend/src/Toner.Application/Contracts/Dtos/ContractDto.cs).
class Contract {
  final String id;
  final String clientId;
  final String clientName;
  final String startDate;
  final String? endDate;
  final String status; // Activo|Vencido|Cancelado
  final int? includedPrintsPerMonth;
  final double? pricePerExtraPage;
  final String? notes;
  final int assetCount;
  final List<String> cityNames;
  final String createdAt;

  Contract({
    required this.id,
    required this.clientId,
    required this.clientName,
    required this.startDate,
    required this.endDate,
    required this.status,
    required this.includedPrintsPerMonth,
    required this.pricePerExtraPage,
    required this.notes,
    required this.assetCount,
    required this.cityNames,
    required this.createdAt,
  });

  factory Contract.fromJson(Map<String, dynamic> json) => Contract(
    id: json['id'] as String,
    clientId: json['clientId'] as String,
    clientName: json['clientName'] as String,
    startDate: json['startDate'] as String,
    endDate: json['endDate'] as String?,
    status: json['status'] as String,
    includedPrintsPerMonth: json['includedPrintsPerMonth'] as int?,
    pricePerExtraPage: (json['pricePerExtraPage'] as num?)?.toDouble(),
    notes: json['notes'] as String?,
    assetCount: json['assetCount'] as int? ?? 0,
    cityNames: (json['cityNames'] as List<dynamic>? ?? []).cast<String>(),
    createdAt: json['createdAt'] as String,
  );
}
