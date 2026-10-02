/// BI de consumo de tóner (espejo de frontend-web/src/api/tonerAnalytics.ts y de TonerBiView.vue). Solo
/// Administrador. Sin precios ni costos por decisión de negocio: mide unidades y duración.
library;

/// Filtros del BI. Las fechas son días calendario locales; se envían como inicio/fin de día para no dejar fuera los
/// registros de hoy.
class TonerFilter {
  final DateTime? from;
  final DateTime? to;
  final String? zoneId;
  final String? clientId;
  final String? brandId;
  final String? modelId;

  const TonerFilter({
    this.from,
    this.to,
    this.zoneId,
    this.clientId,
    this.brandId,
    this.modelId,
  });

  /// Rango por defecto de la web: de hace 6 meses a hoy.
  factory TonerFilter.defaultRange(DateTime now) {
    final today = DateTime(now.year, now.month, now.day);
    return TonerFilter(
      from: DateTime(today.year, today.month - 6, today.day),
      to: today,
    );
  }

  /// null (o vacío) en un campo lo quita del filtro.
  TonerFilter copyWith({
    DateTime? from,
    DateTime? to,
    Object? zoneId = _keep,
    Object? clientId = _keep,
    Object? brandId = _keep,
    Object? modelId = _keep,
  }) => TonerFilter(
    from: from ?? this.from,
    to: to ?? this.to,
    zoneId: identical(zoneId, _keep) ? this.zoneId : zoneId as String?,
    clientId: identical(clientId, _keep) ? this.clientId : clientId as String?,
    brandId: identical(brandId, _keep) ? this.brandId : brandId as String?,
    modelId: identical(modelId, _keep) ? this.modelId : modelId as String?,
  );

  static const _keep = Object();

  /// Hay algo distinto al rango de fechas filtrando.
  bool get hasDimensionFilters =>
      _has(zoneId) || _has(clientId) || _has(brandId) || _has(modelId);

  static bool _has(String? v) => v != null && v.isNotEmpty;

  /// Parámetros de query: solo los que tienen valor ("2026-03-01T00:00:00" / "2026-03-31T23:59:59", sin zona).
  Map<String, String> toQueryParams() => {
    if (from != null) 'from': '${_day(from!)}T00:00:00',
    if (to != null) 'to': '${_day(to!)}T23:59:59',
    if (_has(zoneId)) 'zoneId': zoneId!,
    if (_has(clientId)) 'clientId': clientId!,
    if (_has(brandId)) 'brandId': brandId!,
    if (_has(modelId)) 'modelId': modelId!,
  };

  static String _day(DateTime d) =>
      '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

  @override
  bool operator ==(Object other) =>
      other is TonerFilter &&
      other.from == from &&
      other.to == to &&
      other.zoneId == zoneId &&
      other.clientId == clientId &&
      other.brandId == brandId &&
      other.modelId == modelId;

  @override
  int get hashCode => Object.hash(from, to, zoneId, clientId, brandId, modelId);
}

class TonerMonth {
  /// yyyy-MM
  final String month;
  final int units;

  const TonerMonth({required this.month, required this.units});

  factory TonerMonth.fromJson(Map<String, dynamic> json) => TonerMonth(
    month: json['month'] as String? ?? '',
    units: (json['units'] as num?)?.toInt() ?? 0,
  );
}

class TonerGroupRow {
  final String name;
  final int machines;
  final int totalUnits;
  final double? avgPagesPerUnit;

  const TonerGroupRow({
    required this.name,
    required this.machines,
    required this.totalUnits,
    required this.avgPagesPerUnit,
  });

  factory TonerGroupRow.fromJson(Map<String, dynamic> json) => TonerGroupRow(
    name: json['name'] as String? ?? '',
    machines: (json['machines'] as num?)?.toInt() ?? 0,
    totalUnits: (json['totalUnits'] as num?)?.toInt() ?? 0,
    avgPagesPerUnit: (json['avgPagesPerUnit'] as num?)?.toDouble(),
  );
}

class TonerSummary {
  final int totalUnits;
  final int changedByTechnicianUnits;
  final int deliveredToUserUnits;
  final int machines;
  final int measuredUnits;
  final double? avgPagesPerUnit;
  final List<TonerMonth> monthly;
  final List<TonerGroupRow> byClient;
  final List<TonerGroupRow> byZone;
  final List<TonerGroupRow> byModel;

  const TonerSummary({
    required this.totalUnits,
    required this.changedByTechnicianUnits,
    required this.deliveredToUserUnits,
    required this.machines,
    required this.measuredUnits,
    required this.avgPagesPerUnit,
    required this.monthly,
    required this.byClient,
    required this.byZone,
    required this.byModel,
  });

  factory TonerSummary.fromJson(Map<String, dynamic> json) {
    List<T> list<T>(String key, T Function(Map<String, dynamic>) fromJson) =>
        (json[key] as List<dynamic>? ?? [])
            .map((e) => fromJson(e as Map<String, dynamic>))
            .toList();
    return TonerSummary(
      totalUnits: (json['totalUnits'] as num?)?.toInt() ?? 0,
      changedByTechnicianUnits:
          (json['changedByTechnicianUnits'] as num?)?.toInt() ?? 0,
      deliveredToUserUnits:
          (json['deliveredToUserUnits'] as num?)?.toInt() ?? 0,
      machines: (json['machines'] as num?)?.toInt() ?? 0,
      measuredUnits: (json['measuredUnits'] as num?)?.toInt() ?? 0,
      avgPagesPerUnit: (json['avgPagesPerUnit'] as num?)?.toDouble(),
      monthly: list('monthly', TonerMonth.fromJson),
      byClient: list('byClient', TonerGroupRow.fromJson),
      byZone: list('byZone', TonerGroupRow.fromJson),
      byModel: list('byModel', TonerGroupRow.fromJson),
    );
  }
}

class TonerMachineRow {
  final String assetId;
  final String brand;
  final String model;
  final String serialNumber;
  final String? clientName;
  final String? locationName;
  final String? zoneName;
  final int totalUnits;
  final int changedByTechnicianUnits;
  final int deliveredToUserUnits;
  final int measuredUnits;
  final double? avgPagesPerUnit;
  final int? pagesInRange;
  final double? vsModelPercent;
  final DateTime? lastEventAt;
  final int? lastCounter;

  const TonerMachineRow({
    required this.assetId,
    required this.brand,
    required this.model,
    required this.serialNumber,
    required this.clientName,
    required this.locationName,
    required this.zoneName,
    required this.totalUnits,
    required this.changedByTechnicianUnits,
    required this.deliveredToUserUnits,
    required this.measuredUnits,
    required this.avgPagesPerUnit,
    required this.pagesInRange,
    required this.vsModelPercent,
    required this.lastEventAt,
    required this.lastCounter,
  });

  factory TonerMachineRow.fromJson(Map<String, dynamic> json) =>
      TonerMachineRow(
        assetId: json['assetId'] as String? ?? '',
        brand: json['brand'] as String? ?? '',
        model: json['model'] as String? ?? '',
        serialNumber: json['serialNumber'] as String? ?? '',
        clientName: json['clientName'] as String?,
        locationName: json['locationName'] as String?,
        zoneName: json['zoneName'] as String?,
        totalUnits: (json['totalUnits'] as num?)?.toInt() ?? 0,
        changedByTechnicianUnits:
            (json['changedByTechnicianUnits'] as num?)?.toInt() ?? 0,
        deliveredToUserUnits:
            (json['deliveredToUserUnits'] as num?)?.toInt() ?? 0,
        measuredUnits: (json['measuredUnits'] as num?)?.toInt() ?? 0,
        avgPagesPerUnit: (json['avgPagesPerUnit'] as num?)?.toDouble(),
        pagesInRange: (json['pagesInRange'] as num?)?.toInt(),
        vsModelPercent: (json['vsModelPercent'] as num?)?.toDouble(),
        lastEventAt: TonerAnalytics.parseInstant(json['lastEventAt']),
        lastCounter: (json['lastCounter'] as num?)?.toInt(),
      );
}

/// Página de máquinas (modo offset: trae totalCount).
class TonerMachinesPage {
  final List<TonerMachineRow> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final bool hasMore;

  const TonerMachinesPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.hasMore,
  });

  factory TonerMachinesPage.fromJson(Map<String, dynamic> json) {
    final items = (json['items'] as List<dynamic>? ?? [])
        .map((e) => TonerMachineRow.fromJson(e as Map<String, dynamic>))
        .toList();
    return TonerMachinesPage(
      items: items,
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? items.length,
      totalCount: (json['totalCount'] as num?)?.toInt() ?? items.length,
      hasMore: json['hasMore'] as bool? ?? false,
    );
  }
}

/// Cómo rinde una máquina frente al promedio de su modelo.
enum VsModelLevel {
  /// Sin comparación posible (no hay promedio del modelo contra el cual medir).
  none,

  /// < 85 %: gasta tóner de más (revisar la máquina). Rojo.
  worse,

  /// Entre 85 % y 115 %: rinde como el modelo. Neutro.
  neutral,

  /// > 115 %: rinde más que el modelo. Verde.
  better,
}

/// Lógica pura del BI (formato y clasificación) — se prueba en test/toner_analytics_test.dart.
class TonerAnalytics {
  const TonerAnalytics._();

  static const worseBelow = 85.0;
  static const betterAbove = 115.0;

  /// 100 = rinde igual que el promedio del modelo. Los cortes 85 y 115 son exclusivos (85 y 115 exactos son neutros).
  static VsModelLevel classifyVsModel(double? percent) {
    if (percent == null) return VsModelLevel.none;
    if (percent < worseBelow) return VsModelLevel.worse;
    if (percent > betterAbove) return VsModelLevel.better;
    return VsModelLevel.neutral;
  }

  static const _monthAbbr = [
    'ene',
    'feb',
    'mar',
    'abr',
    'may',
    'jun',
    'jul',
    'ago',
    'sep',
    'oct',
    'nov',
    'dic',
  ];

  /// "2026-03" -> "mar 26". Si el valor no es yyyy-MM, se devuelve tal cual.
  static String monthLabel(String yyyyMm) {
    final parts = yyyyMm.split('-');
    if (parts.length < 2) return yyyyMm;
    final year = int.tryParse(parts[0]);
    final month = int.tryParse(parts[1]);
    if (year == null || month == null || month < 1 || month > 12) {
      return yyyyMm;
    }
    final yy = (year % 100).toString().padLeft(2, '0');
    return '${_monthAbbr[month - 1]} $yy';
  }

  /// Número al estilo es-CO con a lo sumo un decimal ("1.234,5"); null -> "—".
  static String formatNumber(num? value) {
    if (value == null) return '—';
    final rounded = (value * 10).round() / 10;
    final negative = rounded < 0;
    final abs = rounded.abs();
    final whole = abs.truncate();
    final decimal = ((abs - whole) * 10).round();
    final digits = whole.toString();
    final buffer = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) buffer.write('.');
      buffer.write(digits[i]);
    }
    final text = decimal == 0 ? buffer.toString() : '$buffer,$decimal';
    return negative ? '-$text' : text;
  }

  /// "63" / "112,5%" (el porcentaje del chip "Vs. modelo").
  static String formatPercent(double value) => '${formatNumber(value)}%';

  /// dd/MM/yyyy en hora local; null -> "—".
  static String formatDate(DateTime? value) {
    if (value == null) return '—';
    final d = value.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(d.day)}/${two(d.month)}/${d.year}';
  }

  /// Los instantes del backend vienen en UTC; si llegan sin zona se tratan como UTC.
  static DateTime? parseInstant(Object? raw) {
    if (raw is! String || raw.isEmpty) return null;
    final hasZone = RegExp(r'(Z|[+-]\d{2}:?\d{2})$').hasMatch(raw);
    return DateTime.tryParse(hasZone ? raw : '${raw}Z');
  }

  /// Alto relativo (0..1) de una barra. Un valor positivo nunca queda en 0 para que se vea algo.
  static double barFraction(int value, int max) {
    if (value <= 0 || max <= 0) return 0;
    return (value / max).clamp(0.04, 1.0);
  }

  /// Texto para lectores de pantalla: "Tóner usado por mes. ene 26: 5. feb 26: 8."
  static String chartSemanticLabel(List<TonerMonth> monthly) {
    if (monthly.isEmpty) return 'Tóner usado por mes. Sin datos.';
    final parts = monthly.map((m) => '${monthLabel(m.month)}: ${m.units}');
    return 'Tóner usado por mes. ${parts.join('. ')}.';
  }

  /// Resumen "N cambiados por técnico · M entregados al usuario".
  static String splitNote(int technician, int user) =>
      '$technician cambiados por técnico · $user entregados al usuario';
}
