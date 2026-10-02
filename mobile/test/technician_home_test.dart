import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/technician_home.dart';

Map<String, dynamic> job(
  String id, {
  String priority = 'Media',
  bool inProgress = false,
  String kind = 'Ticket',
}) => {
  'kind': kind,
  'id': id,
  'title': 'Ricoh MP2553',
  'summary': 'Falla de arrastre',
  'clientName': 'Electrohuila',
  'locationName': 'Sede Sur',
  'cityName': 'Pitalito',
  'address': 'Calle 1 # 2-3',
  'latitude': 1.85,
  'longitude': -76.05,
  'priority': priority,
  'status': inProgress ? 'EnProceso' : 'Asignado',
  'inProgress': inProgress,
  'since': '2026-10-02T10:00:00Z',
};

void main() {
  group('TechnicianHome.fromJson', () {
    test('lee todos los campos', () {
      final home = TechnicianHome.fromJson({
        'zoneNames': ['Huila - Zona sur'],
        'isWorkingNow': true,
        'todayShift': '08:00–17:00',
        'visitsClosedToday': 2,
        'minutesWorkedToday': 80,
        'activeVisit': job('a', inProgress: true),
        'activeVisitStartedAt': '2026-10-02T11:00:00Z',
        'agenda': [job('a', inProgress: true), job('b', priority: 'Alta')],
        'lowStock': [
          {
            'itemName': 'Fusor',
            'locationName': 'Sur',
            'quantity': -2,
            'minimumStock': 0,
          },
        ],
      });

      expect(home.zoneNames, ['Huila - Zona sur']);
      expect(home.isWorkingNow, isTrue);
      expect(home.visitsClosedToday, 2);
      expect(home.activeVisit!.id, 'a');
      expect(home.lowStock.single.isNegative, isTrue);
      expect(home.agenda.first.latitude, 1.85);
    });

    test('tolera campos opcionales ausentes', () {
      final home = TechnicianHome.fromJson({});

      expect(home.agenda, isEmpty);
      expect(home.activeVisit, isNull);
      expect(home.timeOffUntil, isNull);
      expect(home.nextJob, isNull);
    });

    test(
      'el siguiente trabajo y el resto de la agenda saltan la visita en curso',
      () {
        final home = TechnicianHome.fromJson({
          'activeVisit': job('a', inProgress: true),
          'agenda': [job('a', inProgress: true), job('b'), job('c'), job('d')],
        });

        expect(home.nextJob!.id, 'b');
        expect(home.restOfAgenda.map((j) => j.id), ['c', 'd']);
      },
    );

    test('sin visita en curso, el siguiente es el primero de la agenda', () {
      final home = TechnicianHome.fromJson({
        'agenda': [job('x', priority: 'Critica'), job('y')],
      });

      expect(home.nextJob!.id, 'x');
      expect(home.restOfAgenda.map((j) => j.id), ['y']);
    });
  });

  group('formato', () {
    test('workedLabel', () {
      expect(workedLabel(0), '0 min');
      expect(workedLabel(45), '45 min');
      expect(workedLabel(60), '1 h');
      expect(workedLabel(80), '1 h 20 min');
    });

    test('elapsedLabel', () {
      expect(elapsedLabel(const Duration(seconds: 65)), '01:05');
      expect(
        elapsedLabel(const Duration(hours: 1, minutes: 2, seconds: 3)),
        '1:02:03',
      );
      expect(elapsedLabel(const Duration(seconds: -5)), '00:00');
    });

    test('ageLabel', () {
      final now = DateTime.utc(2026, 10, 2, 12);
      expect(ageLabel(now.subtract(const Duration(seconds: 20)), now), 'ahora');
      expect(
        ageLabel(now.subtract(const Duration(minutes: 5)), now),
        'hace 5 min',
      );
      expect(ageLabel(now.subtract(const Duration(hours: 3)), now), 'hace 3 h');
      expect(
        ageLabel(now.subtract(const Duration(days: 1, hours: 2)), now),
        'hace 1 día',
      );
      expect(
        ageLabel(now.subtract(const Duration(days: 4)), now),
        'hace 4 días',
      );
    });

    test('placeLabel omite lo que falta', () {
      final full = HomeJob.fromJson(job('a'));
      expect(full.placeLabel, 'Electrohuila — Sede Sur · Pitalito');

      final onlyCity = HomeJob.fromJson({
        ...job('a'),
        'clientName': null,
        'locationName': null,
      });
      expect(onlyCity.placeLabel, 'Pitalito');
    });
  });

  group('directionsUri', () {
    test('usa las coordenadas cuando existen', () {
      final uri = directionsUri(HomeJob.fromJson(job('a')))!;
      expect(uri.toString(), contains('destination=1.85,-76.05'));
    });

    test('sin coordenadas busca por dirección y ciudad', () {
      final uri = directionsUri(
        HomeJob.fromJson({...job('a'), 'latitude': null, 'longitude': null}),
      )!;
      expect(uri.queryParameters['destination'], 'Calle 1 # 2-3, Pitalito');
    });

    test('sin nada que buscar devuelve null', () {
      final uri = directionsUri(
        HomeJob.fromJson({
          ...job('a'),
          'latitude': null,
          'longitude': null,
          'address': null,
          'cityName': null,
        }),
      );
      expect(uri, isNull);
    });
  });
}
