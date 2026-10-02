/// Foto de evidencia (antes / después) de un ticket u orden. El contenido se pide aparte, autenticado.
class EvidenceItem {
  EvidenceItem({required this.id, required this.kind, required this.uploadedAt});

  final String id;
  final String kind; // Antes | Despues | Contador
  final DateTime uploadedAt;

  factory EvidenceItem.fromJson(Map<String, dynamic> json) => EvidenceItem(
    id: json['id'] as String,
    kind: json['kind'] as String,
    uploadedAt: DateTime.parse(json['uploadedAt'] as String).toLocal(),
  );
}
