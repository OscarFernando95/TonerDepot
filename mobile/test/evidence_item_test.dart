import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/evidence.dart';

// AUDIT.md H-44: una foto purgada por la política de retención llega con contentPurgedAt y la galería no la descarga.
void main() {
  test('foto con archivo: no está purgada', () {
    final item = EvidenceItem.fromJson({
      'id': 'a',
      'kind': 'Antes',
      'uploadedAt': '2026-10-01T10:00:00Z',
      'contentPurgedAt': null,
    });
    expect(item.isPurged, isFalse);
  });

  test('foto purgada: lo indica', () {
    final item = EvidenceItem.fromJson({
      'id': 'b',
      'kind': 'Despues',
      'uploadedAt': '2026-06-01T10:00:00Z',
      'contentPurgedAt': '2026-10-01T03:00:00Z',
    });
    expect(item.isPurged, isTrue);
  });

  test('respuesta de una API anterior sin el campo: no está purgada', () {
    final item = EvidenceItem.fromJson({
      'id': 'c',
      'kind': 'Contador',
      'uploadedAt': '2026-10-01T10:00:00Z',
    });
    expect(item.isPurged, isFalse);
  });
}
