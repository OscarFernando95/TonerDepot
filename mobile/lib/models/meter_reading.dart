/// Espejo de MeterReadingDto (frontend-web/src/api/types.ts) — una lectura
/// de contador dentro del historial de UN activo puntual (GET/POST
/// /assets/{id}/meter-readings, StaffRoles). Distinto de MeterReadingAsset
/// (mobile/lib/models/meter_reading_asset.dart), que es la fila del flujo de
/// registro del técnico (POST /meter-readings/{assetId}) — ese es otro caso
/// de uso, no se toca acá.
class MeterReading {
  final String id;
  final String readingDate;
  final int counterValue;
  final String? registeredByUserName;

  MeterReading({
    required this.id,
    required this.readingDate,
    required this.counterValue,
    required this.registeredByUserName,
  });

  factory MeterReading.fromJson(Map<String, dynamic> json) => MeterReading(
    id: json['id'] as String,
    readingDate: json['readingDate'] as String,
    counterValue: json['counterValue'] as int,
    registeredByUserName: json['registeredByUserName'] as String?,
  );
}
