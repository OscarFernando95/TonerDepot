import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/asset.dart';
import 'package:toner_tecnico/models/maintenance_order.dart';
import 'package:toner_tecnico/models/status_labels.dart';
import 'package:toner_tecnico/state/my_assets_state.dart';

Map<String, dynamic> _orderJson({
  String id = 'o1',
  String status = 'Pendiente',
  String scheduledDate = '2026-11-05T00:00:00',
  Map<String, dynamic> extra = const {},
}) => {
  'id': id,
  'maintenanceScheduleId': 's1',
  'assetId': 'a1',
  'assetBrandName': 'HP',
  'assetModel': 'M404',
  'assetSerialNumber': 'SN-1',
  'status': status,
  'scheduledDate': scheduledDate,
  'createdAt': '2026-10-01T00:00:00',
  ...extra,
};

Map<String, dynamic> _assetJson(String id, String lifecycle) => {
  'id': id,
  'assetModelId': 'm1',
  'assetBrandName': 'HP',
  'model': 'M404',
  'serialNumber': 'SN-$id',
  'type': 'Impresora',
  'lifecycleStatus': lifecycle,
  'createdAt': '2026-10-01T00:00:00',
};

void main() {
  group('MaintenanceOrder (portal Cliente)', () {
    test('fromJson tolera lo que el backend omite para el cliente', () {
      final o = MaintenanceOrder.fromJson(_orderJson());
      expect(o.technicianName, isNull);
      expect(o.reason, isNull);
      expect(o.completedAt, isNull);
      expect(o.clientLocationName, isNull);
      expect(o.includesGeneral, isFalse);
    });

    test('includesLabels sigue el orden general / unidades / insumos', () {
      final all = MaintenanceOrder.fromJson(
        _orderJson(
          extra: {
            'includesGeneral': true,
            'includesUnits': true,
            'includesConsumables': true,
          },
        ),
      );
      expect(all.includesLabels, [
        'Mantenimiento general',
        'Unidades',
        'Insumos',
      ]);

      final some = MaintenanceOrder.fromJson(
        _orderJson(
          extra: {'includesConsumables': true, 'includesGeneral': true},
        ),
      );
      expect(some.includesLabels, ['Mantenimiento general', 'Insumos']);

      expect(MaintenanceOrder.fromJson(_orderJson()).includesLabels, isEmpty);
    });

    test('isOpen: solo Completada y Cancelada están cerradas', () {
      for (final s in ['Pendiente', 'Asignada', 'EnProceso']) {
        expect(MaintenanceOrder.fromJson(_orderJson(status: s)).isOpen, isTrue);
      }
      for (final s in ['Completada', 'Cancelada']) {
        expect(
          MaintenanceOrder.fromJson(_orderJson(status: s)).isOpen,
          isFalse,
        );
      }
    });

    test('sortForClient: abiertas por fecha ascendente, luego cerradas recientes primero', () {
      final orders = [
        MaintenanceOrder.fromJson(
          _orderJson(
            id: 'cerrada-vieja',
            status: 'Completada',
            scheduledDate: '2026-01-10T00:00:00',
          ),
        ),
        MaintenanceOrder.fromJson(
          _orderJson(id: 'lejana', scheduledDate: '2026-12-01T00:00:00'),
        ),
        MaintenanceOrder.fromJson(
          _orderJson(
            id: 'cerrada-reciente',
            status: 'Cancelada',
            scheduledDate: '2026-09-10T00:00:00',
          ),
        ),
        MaintenanceOrder.fromJson(
          _orderJson(
            id: 'proxima',
            status: 'Asignada',
            scheduledDate: '2026-11-01T00:00:00',
          ),
        ),
      ];
      expect(MaintenanceOrder.sortForClient(orders).map((o) => o.id), [
        'proxima',
        'lejana',
        'cerrada-reciente',
        'cerrada-vieja',
      ]);
    });

    test('formatScheduledDate', () {
      expect(
        MaintenanceOrder.formatScheduledDate('2026-10-22T00:00:00'),
        '22/10/2026',
      );
      expect(MaintenanceOrder.formatScheduledDate('2026-10-22'), '22/10/2026');
      expect(MaintenanceOrder.formatScheduledDate('raro'), 'raro');
    });

    test('etiquetas de estado para el cliente cubren todos los estados', () {
      expect(StatusLabels.clientOrder.keys, StatusLabels.order.keys);
      expect(StatusLabels.clientOrder['Pendiente'], 'Programada');
      expect(StatusLabels.clientOrder['EnProceso'], 'En curso');
    });
  });

  group('MyAssetsState.partition', () {
    test('separa los pendientes de instalar de los ya instalados', () {
      final r = MyAssetsState.partition([
        Asset.fromJson(_assetJson('a', 'Instalado')),
        Asset.fromJson(_assetJson('b', 'EnMantenimiento')),
        Asset.fromJson(_assetJson('c', 'PendienteInstalacion')),
        Asset.fromJson(_assetJson('d', 'PendienteInstalacion')),
      ]);
      expect(r.installed.map((a) => a.id), ['a', 'b']);
      expect(r.pending.map((a) => a.id), ['c', 'd']);
    });

    test('sin pendientes, la lista de pendientes queda vacía', () {
      final r = MyAssetsState.partition([
        Asset.fromJson(_assetJson('a', 'Instalado')),
      ]);
      expect(r.pending, isEmpty);
      expect(r.installed, hasLength(1));
    });
  });
}
