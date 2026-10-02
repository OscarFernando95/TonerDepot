import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/services/push_service.dart';

void main() {
  const id = '3f2b8c1e-9a4d-4e6f-8b7a-1c2d3e4f5a6b';

  test('ticket asignado abre su detalle', () {
    expect(
      pushTargetPath({'type': 'ticket', 'id': id, 'event': 'assigned'}),
      '/tickets/$id',
    );
  });

  test('orden asignada abre su detalle', () {
    expect(
      pushTargetPath({'type': 'order', 'id': id, 'event': 'assigned'}),
      '/maintenance-orders/$id',
    );
  });

  test('ticket sin asignar (staff) abre el detalle', () {
    expect(
      pushTargetPath({'type': 'ticket', 'id': id, 'event': 'unassigned'}),
      '/tickets/$id',
    );
  });

  test('si el servicio ya no es mío, se abre Mi trabajo', () {
    expect(
      pushTargetPath({'type': 'ticket', 'id': id, 'event': 'removed'}),
      '/my-work',
    );
    expect(
      pushTargetPath({'type': 'order', 'id': id, 'event': 'cancelled'}),
      '/my-work',
    );
  });

  test('un payload raro no navega a ninguna parte', () {
    expect(pushTargetPath({}), isNull);
    expect(
      pushTargetPath({'type': 'user', 'id': id, 'event': 'assigned'}),
      isNull,
    );
    expect(pushTargetPath({'type': 'ticket', 'event': 'assigned'}), isNull);
  });

  test('un id que no es un guid nunca llega a la ruta', () {
    expect(
      pushTargetPath({
        'type': 'ticket',
        'id': '../users/new',
        'event': 'assigned',
      }),
      isNull,
    );
    expect(
      pushTargetPath({
        'type': 'ticket',
        'id': '$id/../../x',
        'event': 'assigned',
      }),
      isNull,
    );
  });
}
