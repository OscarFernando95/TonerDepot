import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/maintenance_order.dart';

MaintenanceOrder order(String status, {bool? selfStarted}) =>
    MaintenanceOrder.fromJson({
      'id': 'o1',
      'maintenanceScheduleId': 's',
      'assetId': 'a',
      'assetBrandName': 'Ricoh',
      'assetModel': 'MP2553',
      'assetSerialNumber': 'X1',
      'status': status,
      'isManual': true,
      'isSelfStarted': ?selfStarted,
      'scheduledDate': '2026-10-01',
      'createdAt': '2026-10-01T10:00:00Z',
    });

void main() {
  test(
    'solo la que inició el propio técnico y sigue Asignada se puede cancelar',
    () {
      expect(
        order('Asignada', selfStarted: true).canCancelByTechnician,
        isTrue,
      );
    },
  );

  test('una orden del staff o del cronograma no la cancela el técnico', () {
    expect(
      order('Asignada', selfStarted: false).canCancelByTechnician,
      isFalse,
    );
  });

  test('una respuesta sin el campo (servidor anterior) cuenta como no autoiniciada', () {
    expect(order('Asignada').canCancelByTechnician, isFalse);
  });

  test('ya iniciada o cerrada no se cancela desde aquí', () {
    for (final status in [
      'EnProceso',
      'Completada',
      'Cancelada',
      'Pendiente',
    ]) {
      expect(
        order(status, selfStarted: true).canCancelByTechnician,
        isFalse,
        reason: status,
      );
    }
  });
}
