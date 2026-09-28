/// Espejo de MeterReadingAssetDto (frontend-web/src/api/types.ts) — un
/// equipo instalado, con la última lectura de contador conocida.
class MeterReadingAsset {
  final String assetId;
  final String assetBrandName;
  final String model;
  final String serialNumber;
  final String? clientId;
  final String? clientName;
  final String? clientLocationName;
  final String? area;
  final String? cityName;
  final double? lastMeterReading;

  MeterReadingAsset({
    required this.assetId,
    required this.assetBrandName,
    required this.model,
    required this.serialNumber,
    required this.clientId,
    required this.clientName,
    required this.clientLocationName,
    required this.area,
    required this.cityName,
    required this.lastMeterReading,
  });

  factory MeterReadingAsset.fromJson(Map<String, dynamic> json) =>
      MeterReadingAsset(
        assetId: json['assetId'] as String,
        assetBrandName: json['assetBrandName'] as String? ?? '',
        model: json['model'] as String? ?? '',
        serialNumber: json['serialNumber'] as String? ?? '',
        clientId: json['clientId'] as String?,
        clientName: json['clientName'] as String?,
        clientLocationName: json['clientLocationName'] as String?,
        area: json['area'] as String?,
        cityName: json['cityName'] as String?,
        lastMeterReading: (json['lastMeterReading'] as num?)?.toDouble(),
      );
}
