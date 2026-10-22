import 'client_location.dart';
import 'managed_user.dart';
import 'role_names.dart';

/// Estado del control "Acceso a sedes" de un usuario Cliente: o todas las
/// sedes de su empresa (`restricted == false`, lo que el backend entiende como
/// `allowedLocationIds = null`) o solo las elegidas. Lógica pura, sin Flutter,
/// compartida por la pantalla de crear usuario y la hoja de "Cambiar sedes de
/// acceso" del detalle.
class ClientLocationAccess {
  const ClientLocationAccess({
    this.restricted = false,
    this.selectedIds = const {},
  });

  /// Sin restricción: todas las sedes de la empresa (valor por defecto).
  static const all = ClientLocationAccess();

  /// Precarga el control con lo que el usuario tiene hoy en el backend.
  factory ClientLocationAccess.fromUser(ManagedUser user) =>
      user.restrictedToLocations
      ? ClientLocationAccess(
          restricted: true,
          selectedIds: user.allowedLocationIds.toSet(),
        )
      : all;

  final bool restricted;
  final Set<String> selectedIds;

  /// Cambia entre "todas" y "solo algunas". Al volver a "todas" se descarta la
  /// selección: así no queda ninguna sede oculta que se envíe sin querer.
  ClientLocationAccess withRestricted(bool value) => value
      ? ClientLocationAccess(restricted: true, selectedIds: selectedIds)
      : all;

  ClientLocationAccess toggle(String locationId, bool selected) {
    final next = {...selectedIds};
    if (selected) {
      next.add(locationId);
    } else {
      next.remove(locationId);
    }
    return ClientLocationAccess(restricted: restricted, selectedIds: next);
  }

  /// Mensaje de validación, o null si el estado se puede enviar: "Solo
  /// algunas" exige al menos una sede (el backend también lo rechaza con 400).
  String? get error => restricted && selectedIds.isEmpty
      ? 'Elige al menos una sede, o deja "Todas las sedes".'
      : null;

  bool get isValid => error == null;

  /// Lo que va en `allowedLocationIds` del request: null = todas las sedes.
  /// Ordenado para que el payload sea determinista.
  List<String>? get requestIds =>
      restricted ? (selectedIds.toList()..sort()) : null;

  /// Igualdad de contenido (para saber si "Guardar" cambiaría algo).
  bool sameAs(ClientLocationAccess other) =>
      restricted == other.restricted &&
      selectedIds.length == other.selectedIds.length &&
      selectedIds.containsAll(other.selectedIds);

  /// Sedes que se ofrecen en el checklist: las activas, más las inactivas que
  /// ya estaban seleccionadas (para que el administrador pueda quitarlas en
  /// vez de que queden ocultas). Ordenadas por nombre.
  static List<ClientLocation> selectable(
    List<ClientLocation> locations,
    Set<String> selectedIds,
  ) =>
      locations.where((l) => l.isActive || selectedIds.contains(l.id)).toList()
        ..sort((a, b) => a.name.toLowerCase().compareTo(b.name.toLowerCase()));
}

/// Etiqueta completa para el detalle: "Todas las sedes" o "N sedes: A, B, C y
/// 2 más". `names` mapea id → nombre de sede; si aún no cargó (o falta alguno)
/// se muestra solo el conteo. null si el usuario no es Cliente.
String? clientLocationAccessLabel(
  ManagedUser user,
  Map<String, String> names, {
  int maxNames = 3,
}) {
  if (user.roleName != RoleNames.cliente) return null;
  if (!user.restrictedToLocations) return 'Todas las sedes';
  final count = user.allowedLocationIds.length;
  final noun = count == 1 ? 'sede' : 'sedes';
  final shown = [
    for (final id in user.allowedLocationIds)
      if (names[id] != null) names[id]!,
  ]..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
  if (shown.isEmpty) return '$count $noun';
  final head = shown.take(maxNames).toList();
  final rest = count - head.length;
  return rest > 0
      ? '$count $noun: ${head.join(', ')} y $rest más'
      : '$count $noun: ${head.join(', ')}';
}

/// Versión compacta para la lista: "Todas las sedes" / "N sedes". null si no
/// es un usuario Cliente.
String? clientLocationAccessCompact(ManagedUser user) {
  if (user.roleName != RoleNames.cliente) return null;
  if (!user.restrictedToLocations) return 'Todas las sedes';
  final count = user.allowedLocationIds.length;
  return count == 1 ? '1 sede' : '$count sedes';
}
