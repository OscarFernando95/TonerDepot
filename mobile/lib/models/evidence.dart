/// Foto de evidencia (antes / después) de un ticket u orden. El contenido se pide aparte, autenticado.
class EvidenceItem {
  EvidenceItem({
    required this.id,
    required this.kind,
    required this.uploadedAt,
    this.contentPurgedAt,
  });

  final String id;
  final String kind; // Antes | Despues | Contador
  final DateTime uploadedAt;

  /// Con valor: el archivo se borró por la política de retención (AUDIT.md H-44) y ya no se puede descargar.
  final DateTime? contentPurgedAt;

  bool get isPurged => contentPurgedAt != null;

  factory EvidenceItem.fromJson(Map<String, dynamic> json) => EvidenceItem(
    id: json['id'] as String,
    kind: json['kind'] as String,
    uploadedAt: DateTime.parse(json['uploadedAt'] as String).toLocal(),
    contentPurgedAt: json['contentPurgedAt'] == null
        ? null
        : DateTime.parse(json['contentPurgedAt'] as String).toLocal(),
  );
}
