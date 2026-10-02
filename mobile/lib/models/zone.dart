/// Espejo de ZoneDto / ZoneCityDto / TechnicianZoneDto
/// (backend/src/Toner.Application/Zones y Technicians/Dtos). Una zona agrupa
/// municipios; un municipio pertenece como máximo a una zona.
class ZoneCity {
  final String id;
  final String name;
  final String stateOrProvince;

  const ZoneCity({
    required this.id,
    required this.name,
    required this.stateOrProvince,
  });

  factory ZoneCity.fromJson(Map<String, dynamic> json) => ZoneCity(
    id: json['id'] as String,
    name: json['name'] as String,
    stateOrProvince: json['stateOrProvince'] as String? ?? '',
  );
}

class Zone {
  final String id;
  final String name;
  final int technicianCount;
  final List<ZoneCity> cities;

  const Zone({
    required this.id,
    required this.name,
    required this.technicianCount,
    required this.cities,
  });

  factory Zone.fromJson(Map<String, dynamic> json) => Zone(
    id: json['id'] as String,
    name: json['name'] as String,
    technicianCount: json['technicianCount'] as int? ?? 0,
    cities: (json['cities'] as List<dynamic>? ?? [])
        .map((e) => ZoneCity.fromJson(e as Map<String, dynamic>))
        .toList(),
  );
}

/// Zona asignada a un técnico, con los nombres de los municipios que cubre.
class TechnicianZone {
  final String zoneId;
  final String zoneName;
  final List<String> cityNames;

  const TechnicianZone({
    required this.zoneId,
    required this.zoneName,
    required this.cityNames,
  });

  factory TechnicianZone.fromJson(Map<String, dynamic> json) => TechnicianZone(
    zoneId: json['zoneId'] as String,
    zoneName: json['zoneName'] as String,
    cityNames: (json['cityNames'] as List<dynamic>? ?? []).cast<String>(),
  );
}

/// Lógica pura de selección de zonas de un técnico. El backend reemplaza el
/// conjunto completo (PUT), así que cada acción devuelve la nueva lista de ids.
class ZoneSelection {
  const ZoneSelection._();

  static List<String> add(List<String> current, String zoneId) =>
      current.contains(zoneId) ? List.of(current) : [...current, zoneId];

  static List<String> remove(List<String> current, String zoneId) =>
      current.where((id) => id != zoneId).toList();

  /// Sustituye `oldZoneId` por `newZoneId` conservando el orden; sin duplicados.
  static List<String> replace(
    List<String> current,
    String oldZoneId,
    String newZoneId,
  ) {
    final result = <String>[];
    for (final id in current) {
      final next = id == oldZoneId ? newZoneId : id;
      if (!result.contains(next)) result.add(next);
    }
    if (!result.contains(newZoneId)) result.add(newZoneId);
    return result;
  }

  /// Zonas que aún se pueden asignar (no asignadas ya), ordenadas por nombre.
  static List<Zone> available(List<Zone> all, List<String> assignedIds) =>
      all.where((z) => !assignedIds.contains(z.id)).toList()
        ..sort((a, b) => a.name.toLowerCase().compareTo(b.name.toLowerCase()));
}

/// Lógica pura del selector de municipios de una zona.
class ZoneCityPicking {
  const ZoneCityPicking._();

  /// Minúsculas y sin tildes, para buscar "bogota" y encontrar "Bogotá".
  static String normalize(String s) {
    const from = 'áàäâéèëêíìïîóòöôúùüûñ';
    const to = 'aaaaeeeeiiiioooouuuun';
    final lower = s.toLowerCase().trim();
    final sb = StringBuffer();
    for (final rune in lower.runes) {
      final ch = String.fromCharCode(rune);
      final i = from.indexOf(ch);
      sb.write(i >= 0 ? to[i] : ch);
    }
    return sb.toString();
  }

  /// Filtra por nombre del municipio o del departamento.
  static List<T> filter<T>(
    List<T> items,
    String query, {
    required String Function(T) name,
    required String Function(T) department,
  }) {
    final q = normalize(query);
    if (q.isEmpty) return items;
    return items
        .where(
          (c) =>
              normalize(name(c)).contains(q) ||
              normalize(department(c)).contains(q),
        )
        .toList();
  }

  /// Mapa cityId -> nombre de la zona que hoy lo tiene, excluyendo `exceptZoneId`.
  static Map<String, String> otherZoneByCity(
    List<Zone> zones,
    String exceptZoneId,
  ) => {
    for (final z in zones)
      if (z.id != exceptZoneId)
        for (final c in z.cities) c.id: z.name,
  };

  /// Cuántos de los municipios elegidos se moverían desde otra zona.
  static int movedCount(Set<String> selected, Map<String, String> other) =>
      selected.where(other.containsKey).length;
}
