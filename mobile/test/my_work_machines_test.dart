import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/maintenance_order.dart';
import 'package:toner_tecnico/models/meter_reading_asset.dart';
import 'package:toner_tecnico/models/my_work_filters.dart';
import 'package:toner_tecnico/models/service_ticket.dart';

ServiceTicket ticket(String id, String status, {String? tech}) =>
    ServiceTicket.fromJson({
      'id': id,
      'clientLocationId': 'loc',
      'clientLocationName': 'Sede',
      'clientId': 'c',
      'clientName': 'Cliente',
      'reportedByUserId': 'u',
      'reportedByUserName': 'Usuario',
      'description': 'Falla',
      'status': status,
      'priority': 'Media',
      'technicianId': tech == null ? null : 't-$tech',
      'technicianName': tech,
      'createdAt': '2026-10-01T10:00:00Z',
    });

MaintenanceOrder order(String id, String status) => MaintenanceOrder.fromJson({
  'id': id,
  'maintenanceScheduleId': 's',
  'assetId': 'a',
  'assetBrandName': 'Ricoh',
  'assetModel': 'MP2553',
  'assetSerialNumber': 'X1',
  'status': status,
  'scheduledDate': '2026-10-01',
  'createdAt': '2026-10-01T10:00:00Z',
});

Map<String, dynamic> machine([Map<String, dynamic> extra = const {}]) => {
  'assetId': 'a1',
  'assetBrandName': 'Ricoh',
  'model': 'MP2553',
  'serialNumber': 'S1',
  ...extra,
};

void main() {
  group('Filtros de Mi trabajo (solo pendientes)', () {
    test('tickets: solo Asignado y EnProceso', () {
      final all = [
        for (final s in [
          'Abierto',
          'SinAsignar',
          'Asignado',
          'EnProceso',
          'Resuelto',
          'Cerrado',
          'Cancelado',
        ])
          ticket(s, s),
      ];
      expect(pendingTickets(all).map((t) => t.id), ['Asignado', 'EnProceso']);
    });

    test('órdenes: solo Asignada y EnProceso', () {
      final all = [
        for (final s in [
          'Pendiente',
          'Asignada',
          'EnProceso',
          'Completada',
          'Cancelada',
        ])
          order(s, s),
      ];
      expect(pendingOrders(all).map((o) => o.id), ['Asignada', 'EnProceso']);
    });

    test('un ticket resuelto desaparece al recalcular', () {
      var list = [ticket('1', 'EnProceso'), ticket('2', 'Asignado')];
      expect(pendingTickets(list), hasLength(2));
      list = [ticket('1', 'Resuelto'), ticket('2', 'Asignado')];
      expect(pendingTickets(list).map((t) => t.id), ['2']);
    });

    test('etiqueta de quién tiene el ticket', () {
      expect(ticketAssigneeLabel(ticket('1', 'SinAsignar')), 'Sin asignar');
      expect(
        ticketAssigneeLabel(ticket('2', 'Asignado', tech: 'Ana Pérez')),
        'Ana Pérez',
      );
    });
  });

  group('MeterReadingAsset: último tóner', () {
    test('tolera la ausencia de los campos nuevos', () {
      final a = MeterReadingAsset.fromJson(machine());
      expect(a.lastTonerAt, isNull);
      expect(a.lastTonerCounter, isNull);
      expect(a.tonerUnitsLast90Days, 0);
      expect(a.lastTonerLabel, 'Sin tóner registrado');
    });

    test('lee los campos y arma la etiqueta', () {
      final a = MeterReadingAsset.fromJson(
        machine({
          'lastTonerAt': '2026-09-28T15:00:00Z',
          'lastTonerCounter': 12345,
          'tonerUnitsLast90Days': 2,
        }),
      );
      expect(a.lastTonerCounter, 12345);
      expect(a.tonerUnitsLast90Days, 2);
      expect(
        a.lastTonerLabel,
        'Último tóner: 28/09/2026 · contador 12345 · 2 en 90 días',
      );
    });

    test(
      'sin contador omite esa parte; fecha inválida cuenta como sin tóner',
      () {
        final a = MeterReadingAsset.fromJson(
          machine({
            'lastTonerAt': '2026-09-28T15:00:00Z',
            'tonerUnitsLast90Days': 1,
          }),
        );
        expect(a.lastTonerLabel, 'Último tóner: 28/09/2026 · 1 en 90 días');
        final b = MeterReadingAsset.fromJson(
          machine({'lastTonerAt': 'basura'}),
        );
        expect(b.lastTonerLabel, 'Sin tóner registrado');
      },
    );
  });
}
