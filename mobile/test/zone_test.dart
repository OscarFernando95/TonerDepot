import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/technician.dart';
import 'package:toner_tecnico/models/zone.dart';

void main() {
  group('parseo', () {
    test('Zone.fromJson', () {
      final z = Zone.fromJson({
        'id': 'z1',
        'name': 'Norte',
        'technicianCount': 2,
        'cities': [
          {'id': 'c1', 'name': 'Bogotá', 'stateOrProvince': 'Cundinamarca'},
        ],
      });
      expect(z.name, 'Norte');
      expect(z.technicianCount, 2);
      expect(z.cities.single.stateOrProvince, 'Cundinamarca');
    });

    test('Zone.fromJson tolera cities ausente', () {
      final z = Zone.fromJson({'id': 'z1', 'name': 'Sur'});
      expect(z.cities, isEmpty);
      expect(z.technicianCount, 0);
    });

    test('TechnicianZone.fromJson', () {
      final t = TechnicianZone.fromJson({
        'zoneId': 'z1',
        'zoneName': 'Norte',
        'cityNames': ['Bogotá', 'Chía'],
      });
      expect(t.zoneId, 'z1');
      expect(t.cityNames, ['Bogotá', 'Chía']);
    });

    test('Technician.fromJson lee zoneNames (y tolera su ausencia)', () {
      final base = {'id': 't', 'fullName': 'Ana', 'status': 'Disponible'};
      final t = Technician.fromJson({
        ...base,
        'zoneNames': ['Norte'],
        'coverageCityNames': ['Bogotá'],
      });
      expect(t.zoneNames, ['Norte']);
      expect(t.coverageCityNames, ['Bogotá']);
      expect(Technician.fromJson(base).zoneNames, isEmpty);
    });
  });

  group('ZoneSelection', () {
    test('add no duplica', () {
      expect(ZoneSelection.add(['a'], 'b'), ['a', 'b']);
      expect(ZoneSelection.add(['a'], 'a'), ['a']);
    });

    test('remove', () {
      expect(ZoneSelection.remove(['a', 'b'], 'a'), ['b']);
      expect(ZoneSelection.remove(['a'], 'x'), ['a']);
    });

    test('replace conserva el orden y evita duplicados', () {
      expect(ZoneSelection.replace(['a', 'b'], 'a', 'c'), ['c', 'b']);
      expect(ZoneSelection.replace(['a', 'b'], 'a', 'b'), ['b']);
      expect(ZoneSelection.replace([], 'a', 'c'), ['c']);
    });

    test('available excluye asignadas y ordena por nombre', () {
      const z = [
        Zone(id: '1', name: 'sur', technicianCount: 0, cities: []),
        Zone(id: '2', name: 'Centro', technicianCount: 0, cities: []),
        Zone(id: '3', name: 'Norte', technicianCount: 0, cities: []),
      ];
      expect(ZoneSelection.available(z, ['3']).map((e) => e.id), ['2', '1']);
    });
  });

  group('ZoneCityPicking', () {
    const cities = [
      ZoneCity(id: '1', name: 'Bogotá', stateOrProvince: 'Cundinamarca'),
      ZoneCity(id: '2', name: 'Medellín', stateOrProvince: 'Antioquia'),
    ];
    List<ZoneCity> f(String q) => ZoneCityPicking.filter<ZoneCity>(
      cities,
      q,
      name: (c) => c.name,
      department: (c) => c.stateOrProvince,
    );

    test('normalize quita tildes y mayúsculas', () {
      expect(ZoneCityPicking.normalize('  BOGOTÁ '), 'bogota');
      expect(ZoneCityPicking.normalize('Ñandú'), 'nandu');
    });

    test('filter ignora tildes y busca también por departamento', () {
      expect(f('bogota').map((c) => c.id), ['1']);
      expect(f('antio').map((c) => c.id), ['2']);
      expect(f('').length, 2);
      expect(f('zzz'), isEmpty);
    });

    test('otherZoneByCity excluye la zona actual y movedCount cuenta movidos', () {
      const zones = [
        Zone(id: 'a', name: 'A', technicianCount: 0, cities: [ZoneCity(id: '1', name: 'x', stateOrProvince: '')]),
        Zone(id: 'b', name: 'B', technicianCount: 0, cities: [ZoneCity(id: '2', name: 'y', stateOrProvince: '')]),
      ];
      final other = ZoneCityPicking.otherZoneByCity(zones, 'a');
      expect(other, {'2': 'B'});
      expect(ZoneCityPicking.movedCount({'1', '2'}, other), 1);
    });
  });
}
