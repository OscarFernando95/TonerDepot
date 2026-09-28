/// Espejo de AssetDto (backend/src/Toner.Application/Assets/Dtos/AssetDto.cs)
/// — mismo shape para el portal de Cliente (solo lectura) y el catálogo de
/// Staff; el catálogo simplemente usa además assetModelId/currentClientId
/// para editar y para el filtro por cliente.
class Asset {
  final String id;
  final String assetModelId;
  final String assetBrandName;
  final String model;
  final String serialNumber;
  final String type; // Impresora|ComputoEquipo
  final String lifecycleStatus; // EnBodega|Instalado|EnMantenimiento|DadoDeBaja|PendienteInstalacion
  final String? area;
  final String? currentClientLocationId;
  final String? currentClientLocationName;
  final String? currentClientId;
  final String? currentClientName;
  final String? cityName;
  final int? lastMeterReading;
  final String? activeContractId;
  final String createdAt;

  Asset({
    required this.id,
    required this.assetModelId,
    required this.assetBrandName,
    required this.model,
    required this.serialNumber,
    required this.type,
    required this.lifecycleStatus,
    required this.area,
    required this.currentClientLocationId,
    required this.currentClientLocationName,
    required this.currentClientId,
    required this.currentClientName,
    required this.cityName,
    required this.lastMeterReading,
    required this.activeContractId,
    required this.createdAt,
  });

  /// Espejo de AssetAllowedTransitions en frontend-web/src/api/types.ts —
  /// PendienteInstalacion nunca se elige a mano (el backend la alcanza solo).
  static const Map<String, List<String>> allowedTransitions = {
    'EnBodega': ['Instalado', 'PendienteInstalacion', 'DadoDeBaja'],
    'Instalado': ['EnMantenimiento', 'EnBodega', 'DadoDeBaja'],
    'EnMantenimiento': ['Instalado', 'EnBodega', 'DadoDeBaja'],
    'PendienteInstalacion': ['Instalado', 'EnBodega'],
    'DadoDeBaja': [],
  };

  factory Asset.fromJson(Map<String, dynamic> json) => Asset(
    id: json['id'] as String,
    assetModelId: json['assetModelId'] as String,
    assetBrandName: json['assetBrandName'] as String,
    model: json['model'] as String,
    serialNumber: json['serialNumber'] as String,
    type: json['type'] as String,
    lifecycleStatus: json['lifecycleStatus'] as String,
    area: json['area'] as String?,
    currentClientLocationId: json['currentClientLocationId'] as String?,
    currentClientLocationName: json['currentClientLocationName'] as String?,
    currentClientId: json['currentClientId'] as String?,
    currentClientName: json['currentClientName'] as String?,
    cityName: json['cityName'] as String?,
    lastMeterReading: json['lastMeterReading'] as int?,
    activeContractId: json['activeContractId'] as String?,
    createdAt: json['createdAt'] as String,
  );
}
