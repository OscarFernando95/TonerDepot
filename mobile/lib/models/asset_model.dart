/// Los 5 campos de umbral/intervalo son la config que usa el motor de
/// programaciones de mantenimiento (Fase F, módulo Programaciones) — acá
/// solo se editan, no se interpretan.
class AssetModel {
  final String id;
  final String assetBrandId;
  final String name;
  final int generalPrintThreshold;
  final int generalMonthsInterval;
  final int unitsPrintThreshold;
  final int unitsMonthsInterval;
  final int consumablesPrintThreshold;

  AssetModel({
    required this.id,
    required this.assetBrandId,
    required this.name,
    required this.generalPrintThreshold,
    required this.generalMonthsInterval,
    required this.unitsPrintThreshold,
    required this.unitsMonthsInterval,
    required this.consumablesPrintThreshold,
  });

  factory AssetModel.fromJson(Map<String, dynamic> json) => AssetModel(
        id: json['id'] as String,
        assetBrandId: json['assetBrandId'] as String,
        name: json['name'] as String,
        generalPrintThreshold: json['generalPrintThreshold'] as int,
        generalMonthsInterval: json['generalMonthsInterval'] as int,
        unitsPrintThreshold: json['unitsPrintThreshold'] as int,
        unitsMonthsInterval: json['unitsMonthsInterval'] as int,
        consumablesPrintThreshold: json['consumablesPrintThreshold'] as int,
      );
}
