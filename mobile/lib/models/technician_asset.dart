/// Espejo de TechnicianAssetDto (backend/src/Toner.Application/Technicians/Dtos/TechnicianAssetDto.cs).
/// Un activo vinculado explícitamente a un técnico — gobierna qué ve en
/// "Lectura de contadores" (ver AssetService.ListForMeterReadingAsync).
class TechnicianAsset {
  final String id;
  final String assetId;
  final String assetBrandName;
  final String model;
  final String serialNumber;
  final String? clientName;
  final String? clientLocationName;
  final String? cityName;
  final String? area;

  TechnicianAsset({
    required this.id,
    required this.assetId,
    required this.assetBrandName,
    required this.model,
    required this.serialNumber,
    required this.clientName,
    required this.clientLocationName,
    required this.cityName,
    required this.area,
  });

  factory TechnicianAsset.fromJson(Map<String, dynamic> json) => TechnicianAsset(
        id: json['id'] as String,
        assetId: json['assetId'] as String,
        assetBrandName: json['assetBrandName'] as String,
        model: json['model'] as String,
        serialNumber: json['serialNumber'] as String,
        clientName: json['clientName'] as String?,
        clientLocationName: json['clientLocationName'] as String?,
        cityName: json['cityName'] as String?,
        area: json['area'] as String?,
      );
}
