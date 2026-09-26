/// Envelope que devuelven todos los listados del backend (ver
/// frontend-web/src/api/paging.ts). totalCount/page solo vienen en modo
/// offset; nextCursor solo en modo cursor (keyset). Para "Mi trabajo" los
/// volúmenes por técnico son chicos, así que por ahora solo usamos `items`
/// y no seguimos la paginación — igual que hacía el frontend web al inicio.
class PagedResult<T> {
  final List<T> items;
  final int pageSize;
  final bool hasMore;

  PagedResult({required this.items, required this.pageSize, required this.hasMore});

  factory PagedResult.fromJson(
    Map<String, dynamic> json,
    T Function(Map<String, dynamic>) fromJsonT,
  ) {
    final rawItems = (json['items'] as List<dynamic>? ?? []);
    return PagedResult(
      items: rawItems.map((e) => fromJsonT(e as Map<String, dynamic>)).toList(),
      pageSize: json['pageSize'] as int? ?? rawItems.length,
      hasMore: json['hasMore'] as bool? ?? false,
    );
  }
}
