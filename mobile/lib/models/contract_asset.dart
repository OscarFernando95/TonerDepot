/// Espejo de ContractAssetDto — el vínculo entre un contrato y un activo
/// (con vigencia propia: startDate/endDate, no las del contrato).
class ContractAsset {
  final String id;
  final String contractId;
  final String assetId;
  final String assetBrandName;
  final String assetModel;
  final String assetSerialNumber;
  final String startDate;
  final String? endDate;
  final String? area;
  final int? lastMeterReading;
  final double? averageMonthlyPrints;

  ContractAsset({
    required this.id,
    required this.contractId,
    required this.assetId,
    required this.assetBrandName,
    required this.assetModel,
    required this.assetSerialNumber,
    required this.startDate,
    required this.endDate,
    required this.area,
    required this.lastMeterReading,
    required this.averageMonthlyPrints,
  });

  bool get isActive => endDate == null;

  factory ContractAsset.fromJson(Map<String, dynamic> json) => ContractAsset(
        id: json['id'] as String,
        contractId: json['contractId'] as String,
        assetId: json['assetId'] as String,
        assetBrandName: json['assetBrandName'] as String,
        assetModel: json['assetModel'] as String,
        assetSerialNumber: json['assetSerialNumber'] as String,
        startDate: json['startDate'] as String,
        endDate: json['endDate'] as String?,
        area: json['area'] as String?,
        lastMeterReading: json['lastMeterReading'] as int?,
        averageMonthlyPrints: (json['averageMonthlyPrints'] as num?)?.toDouble(),
      );
}
