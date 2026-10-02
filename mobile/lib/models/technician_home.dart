/// Trabajo de la agenda del técnico (ticket u orden) o la visita en curso (también instalación).
class HomeJob {
  HomeJob({
    required this.kind,
    required this.id,
    required this.title,
    required this.priority,
    required this.status,
    required this.inProgress,
    required this.since,
    this.summary,
    this.clientName,
    this.locationName,
    this.cityName,
    this.address,
    this.latitude,
    this.longitude,
  });

  final String kind; // Ticket | Orden | Instalación
  final String id;
  final String title;
  final String? summary;
  final String? clientName;
  final String? locationName;
  final String? cityName;
  final String? address;
  final double? latitude;
  final double? longitude;
  final String priority; // Baja | Media | Alta | Critica
  final String status;
  final bool inProgress;
  final DateTime since;

  factory HomeJob.fromJson(Map<String, dynamic> json) => HomeJob(
    kind: json['kind'] as String? ?? 'Ticket',
    id: json['id'] as String,
    title: json['title'] as String? ?? '',
    summary: json['summary'] as String?,
    clientName: json['clientName'] as String?,
    locationName: json['locationName'] as String?,
    cityName: json['cityName'] as String?,
    address: json['address'] as String?,
    latitude: (json['latitude'] as num?)?.toDouble(),
    longitude: (json['longitude'] as num?)?.toDouble(),
    priority: json['priority'] as String? ?? 'Media',
    status: json['status'] as String? ?? '',
    inProgress: json['inProgress'] as bool? ?? false,
    since: DateTime.parse(json['since'] as String).toUtc(),
  );

  /// "Cliente — Sede · Ciudad", omitiendo lo que falte.
  String get placeLabel {
    final parts = <String>[
      if (clientName != null && clientName!.isNotEmpty) clientName!,
      if (locationName != null && locationName!.isNotEmpty) locationName!,
    ];
    final base = parts.join(' — ');
    return cityName != null && cityName!.isNotEmpty
        ? (base.isEmpty ? cityName! : '$base · $cityName')
        : base;
  }
}

class HomeLowStock {
  HomeLowStock({
    required this.itemName,
    required this.locationName,
    required this.quantity,
    required this.minimumStock,
  });

  final String itemName;
  final String locationName;
  final int quantity;
  final int minimumStock;

  bool get isNegative => quantity < 0;

  factory HomeLowStock.fromJson(Map<String, dynamic> json) => HomeLowStock(
    itemName: json['itemName'] as String? ?? '',
    locationName: json['locationName'] as String? ?? '',
    quantity: (json['quantity'] as num?)?.toInt() ?? 0,
    minimumStock: (json['minimumStock'] as num?)?.toInt() ?? 0,
  );
}

/// Resumen del Inicio del técnico (GET /technicians/me/home).
class TechnicianHome {
  TechnicianHome({
    required this.zoneNames,
    required this.isWorkingNow,
    required this.visitsClosedToday,
    required this.minutesWorkedToday,
    required this.agenda,
    required this.lowStock,
    this.timeOffUntil,
    this.todayShift,
    this.activeVisit,
    this.activeVisitStartedAt,
  });

  final List<String> zoneNames;
  final bool isWorkingNow;
  final DateTime? timeOffUntil;
  final String? todayShift;
  final int visitsClosedToday;
  final int minutesWorkedToday;
  final HomeJob? activeVisit;
  final DateTime? activeVisitStartedAt;
  final List<HomeJob> agenda;
  final List<HomeLowStock> lowStock;

  /// Lo que sigue: lo primero de la agenda que no sea la visita en curso.
  HomeJob? get nextJob {
    for (final job in agenda) {
      if (job.id != activeVisit?.id) return job;
    }
    return null;
  }

  /// El resto de la agenda, sin la visita en curso ni el siguiente trabajo.
  List<HomeJob> get restOfAgenda {
    final skip = nextJob?.id;
    return agenda
        .where((j) => j.id != activeVisit?.id && j.id != skip)
        .toList();
  }

  factory TechnicianHome.fromJson(Map<String, dynamic> json) => TechnicianHome(
    zoneNames: (json['zoneNames'] as List<dynamic>? ?? const [])
        .map((e) => e as String)
        .toList(),
    isWorkingNow: json['isWorkingNow'] as bool? ?? false,
    timeOffUntil: json['timeOffUntil'] != null
        ? DateTime.parse(json['timeOffUntil'] as String).toLocal()
        : null,
    todayShift: json['todayShift'] as String?,
    visitsClosedToday: (json['visitsClosedToday'] as num?)?.toInt() ?? 0,
    minutesWorkedToday: (json['minutesWorkedToday'] as num?)?.toInt() ?? 0,
    activeVisit: json['activeVisit'] != null
        ? HomeJob.fromJson(json['activeVisit'] as Map<String, dynamic>)
        : null,
    activeVisitStartedAt: json['activeVisitStartedAt'] != null
        ? DateTime.parse(json['activeVisitStartedAt'] as String).toUtc()
        : null,
    agenda: (json['agenda'] as List<dynamic>? ?? const [])
        .map((e) => HomeJob.fromJson(e as Map<String, dynamic>))
        .toList(),
    lowStock: (json['lowStock'] as List<dynamic>? ?? const [])
        .map((e) => HomeLowStock.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

// ── Formato (puro, para probarlo sin widgets) ───────────────────────────────────────────────────

/// "1 h 20 min", "45 min", "0 min".
String workedLabel(int minutes) {
  if (minutes < 60) return '$minutes min';
  final h = minutes ~/ 60;
  final m = minutes % 60;
  return m == 0 ? '$h h' : '$h h $m min';
}

/// Cronómetro de la visita: "mm:ss" bajo una hora, "h:mm:ss" desde entonces.
String elapsedLabel(Duration d) {
  final total = d.isNegative ? Duration.zero : d;
  final h = total.inHours;
  final m = total.inMinutes.remainder(60).toString().padLeft(2, '0');
  final s = total.inSeconds.remainder(60).toString().padLeft(2, '0');
  return h > 0 ? '$h:$m:$s' : '$m:$s';
}

/// Antigüedad de un trabajo: "hace 5 min", "hace 3 h", "hace 2 días".
String ageLabel(DateTime since, DateTime now) {
  final diff = now.difference(since);
  if (diff.inMinutes < 1) return 'ahora';
  if (diff.inMinutes < 60) return 'hace ${diff.inMinutes} min';
  if (diff.inHours < 24) return 'hace ${diff.inHours} h';
  final days = diff.inDays;
  return days == 1 ? 'hace 1 día' : 'hace $days días';
}

/// Enlace para llegar a la sede: por coordenadas si hay, si no por dirección; null si no hay nada que buscar.
Uri? directionsUri(HomeJob job) {
  if (job.latitude != null && job.longitude != null) {
    return Uri.parse(
      'https://www.google.com/maps/dir/?api=1&destination=${job.latitude},${job.longitude}',
    );
  }
  final query = [
    job.address,
    job.cityName,
  ].where((s) => s != null && s.trim().isNotEmpty).join(', ');
  if (query.isEmpty) return null;
  return Uri.https('www.google.com', '/maps/dir/', {
    'api': '1',
    'destination': query,
  });
}
