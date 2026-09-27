/// Espejo de ClientDto (backend/src/Toner.Application/Clients/Dtos/ClientDto.cs)
/// — mismo shape para lista y detalle, no trae las sedes embebidas (solo el
/// agregado locationCount/cityNames; las sedes se piden aparte, ver
/// client_location_api.dart).
class Client {
  final String id;
  final String name;
  final String? taxId;
  final String? contactName;
  final String? contactEmail;
  final String? contactPhone;
  final bool isActive;
  final bool isContractClient;
  final int locationCount;
  final List<String> cityNames;
  final String createdAt;

  Client({
    required this.id,
    required this.name,
    required this.taxId,
    required this.contactName,
    required this.contactEmail,
    required this.contactPhone,
    required this.isActive,
    required this.isContractClient,
    required this.locationCount,
    required this.cityNames,
    required this.createdAt,
  });

  factory Client.fromJson(Map<String, dynamic> json) => Client(
        id: json['id'] as String,
        name: json['name'] as String,
        taxId: json['taxId'] as String?,
        contactName: json['contactName'] as String?,
        contactEmail: json['contactEmail'] as String?,
        contactPhone: json['contactPhone'] as String?,
        isActive: json['isActive'] as bool? ?? true,
        isContractClient: json['isContractClient'] as bool? ?? true,
        locationCount: json['locationCount'] as int? ?? 0,
        cityNames: (json['cityNames'] as List<dynamic>? ?? []).cast<String>(),
        createdAt: json['createdAt'] as String,
      );
}
