/// Espejo de ClientLocationDto — mismo shape para list/create/update.
class ClientLocation {
  final String id;
  final String clientId;
  final String clientName;
  final String cityId;
  final String cityName;
  final String name;
  final String address;
  final String? contactName;
  final String? contactPhone;
  /// Coordenadas WGS84 de la sede (null si aún no se cargaron): con ellas se verifica que el técnico llegó.
  final double? latitude;
  final double? longitude;
  final bool isActive;
  final String createdAt;

  ClientLocation({
    required this.id,
    required this.clientId,
    required this.clientName,
    required this.cityId,
    required this.cityName,
    required this.name,
    required this.address,
    required this.contactName,
    required this.contactPhone,
    required this.latitude,
    required this.longitude,
    required this.isActive,
    required this.createdAt,
  });

  factory ClientLocation.fromJson(Map<String, dynamic> json) => ClientLocation(
    id: json['id'] as String,
    clientId: json['clientId'] as String,
    clientName: json['clientName'] as String,
    cityId: json['cityId'] as String,
    cityName: json['cityName'] as String,
    name: json['name'] as String,
    address: json['address'] as String,
    contactName: json['contactName'] as String?,
    contactPhone: json['contactPhone'] as String?,
    latitude: (json['latitude'] as num?)?.toDouble(),
    longitude: (json['longitude'] as num?)?.toDouble(),
    isActive: json['isActive'] as bool? ?? true,
    createdAt: json['createdAt'] as String,
  );
}
