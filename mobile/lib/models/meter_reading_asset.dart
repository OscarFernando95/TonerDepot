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

  /// Último tóner registrado para la máquina (instante UTC) y el contador con que se registró; null si nunca se
  /// registró uno.
  final DateTime? lastTonerAt;
  final int? lastTonerCounter;

  /// Unidades de tóner registradas en los últimos 90 días (0 si ninguna).
  final int tonerUnitsLast90Days;

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
    this.lastTonerAt,
    this.lastTonerCounter,
    this.tonerUnitsLast90Days = 0,
  });

  /// Línea de "último tóner" de la tarjeta: 'Último tóner: 28/09/2026 · contador 12345 · 2 en 90 días' o
  /// 'Sin tóner registrado'. Las partes que falten (contador) se omiten.
  String get lastTonerLabel {
    final at = lastTonerAt;
    if (at == null) return 'Sin tóner registrado';
    final d = at.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    final parts = <String>[
      'Último tóner: ${two(d.day)}/${two(d.month)}/${d.year}',
      if (lastTonerCounter != null) 'contador $lastTonerCounter',
      '$tonerUnitsLast90Days en 90 días',
    ];
    return parts.join(' · ');
  }

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
        lastTonerAt: DateTime.tryParse(json['lastTonerAt'] as String? ?? ''),
        lastTonerCounter: (json['lastTonerCounter'] as num?)?.toInt(),
        tonerUnitsLast90Days:
            (json['tonerUnitsLast90Days'] as num?)?.toInt() ?? 0,
      );
}
