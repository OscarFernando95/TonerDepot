import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/client_location.dart';
import 'package:toner_tecnico/models/client_location_access.dart';
import 'package:toner_tecnico/models/managed_user.dart';

Map<String, dynamic> _userJson({
  String roleName = 'Cliente',
  Map<String, dynamic> extra = const {},
}) => {
  'id': 'u1',
  'cedula': '123',
  'fullName': 'Ana',
  'roleName': roleName,
  'createdAt': '2026-01-01T00:00:00Z',
  ...extra,
};

ClientLocation _loc(String id, String name, {bool active = true}) =>
    ClientLocation.fromJson({
      'id': id,
      'clientId': 'c1',
      'clientName': 'Acme',
      'cityId': 'city',
      'cityName': 'Bogotá',
      'name': name,
      'address': 'Calle 1',
      'isActive': active,
      'createdAt': '2026-01-01T00:00:00Z',
    });

void main() {
  group('ManagedUser.fromJson', () {
    test('lee restrictedToLocations y allowedLocationIds', () {
      final u = ManagedUser.fromJson(
        _userJson(
          extra: {
            'restrictedToLocations': true,
            'allowedLocationIds': ['a', 'b'],
          },
        ),
      );
      expect(u.restrictedToLocations, isTrue);
      expect(u.allowedLocationIds, ['a', 'b']);
    });

    test('tolera campos ausentes o null', () {
      final absent = ManagedUser.fromJson(_userJson());
      expect(absent.restrictedToLocations, isFalse);
      expect(absent.allowedLocationIds, isEmpty);

      final nulls = ManagedUser.fromJson(
        _userJson(
          extra: {'restrictedToLocations': null, 'allowedLocationIds': null},
        ),
      );
      expect(nulls.restrictedToLocations, isFalse);
      expect(nulls.allowedLocationIds, isEmpty);
    });
  });

  group('ClientLocationAccess', () {
    test('por defecto son todas las sedes y no envía ids', () {
      expect(ClientLocationAccess.all.restricted, isFalse);
      expect(ClientLocationAccess.all.requestIds, isNull);
      expect(ClientLocationAccess.all.isValid, isTrue);
    });

    test('"Solo algunas" sin sedes es inválido, con ≥1 es válido', () {
      final empty = ClientLocationAccess.all.withRestricted(true);
      expect(empty.isValid, isFalse);
      expect(empty.error, isNotNull);
      final one = empty.toggle('b', true);
      expect(one.isValid, isTrue);
      expect(one.error, isNull);
    });

    test('requestIds va ordenado y solo si está restringido', () {
      final a = ClientLocationAccess.all
          .withRestricted(true)
          .toggle('z', true)
          .toggle('a', true)
          .toggle('m', true);
      expect(a.requestIds, ['a', 'm', 'z']);
      expect(a.toggle('m', false).requestIds, ['a', 'z']);
    });

    test('volver a "todas" descarta la selección', () {
      final a = ClientLocationAccess.all.withRestricted(true).toggle('x', true);
      final back = a.withRestricted(false);
      expect(back.restricted, isFalse);
      expect(back.selectedIds, isEmpty);
      expect(back.requestIds, isNull);
      // y al volver a restringir arranca vacío.
      expect(back.withRestricted(true).selectedIds, isEmpty);
    });

    test('toggle no duplica y no muta el estado anterior', () {
      final a = ClientLocationAccess.all.withRestricted(true).toggle('x', true);
      final b = a.toggle('x', true);
      expect(b.selectedIds, {'x'});
      a.toggle('y', true);
      expect(a.selectedIds, {'x'});
    });

    test('fromUser precarga la restricción actual', () {
      final restricted = ManagedUser.fromJson(
        _userJson(
          extra: {
            'restrictedToLocations': true,
            'allowedLocationIds': ['a', 'b'],
          },
        ),
      );
      final access = ClientLocationAccess.fromUser(restricted);
      expect(access.restricted, isTrue);
      expect(access.selectedIds, {'a', 'b'});

      final open = ClientLocationAccess.fromUser(
        ManagedUser.fromJson(_userJson()),
      );
      expect(open.restricted, isFalse);
    });

    test('sameAs compara contenido', () {
      final a = ClientLocationAccess.all.withRestricted(true).toggle('a', true);
      final b = ClientLocationAccess.all.withRestricted(true).toggle('a', true);
      expect(a.sameAs(b), isTrue);
      expect(a.sameAs(b.toggle('b', true)), isFalse);
      expect(a.sameAs(ClientLocationAccess.all), isFalse);
    });

    test('selectable: activas más inactivas ya elegidas, por nombre', () {
      final locations = [
        _loc('1', 'Sur'),
        _loc('2', 'Centro', active: false),
        _loc('3', 'Norte', active: false),
        _loc('4', 'Este'),
      ];
      final result = ClientLocationAccess.selectable(locations, {'3'});
      expect(result.map((l) => l.name), ['Este', 'Norte', 'Sur']);
    });
  });

  group('etiquetas', () {
    ManagedUser restricted(List<String> ids, {String role = 'Cliente'}) =>
        ManagedUser.fromJson(
          _userJson(
            roleName: role,
            extra: {'restrictedToLocations': true, 'allowedLocationIds': ids},
          ),
        );
    const names = {'a': 'Norte', 'b': 'Sur', 'c': 'Centro', 'd': 'Este'};

    test('no aplica a roles distintos de Cliente', () {
      final tecnico = ManagedUser.fromJson(_userJson(roleName: 'Tecnico'));
      expect(clientLocationAccessLabel(tecnico, names), isNull);
      expect(clientLocationAccessCompact(tecnico), isNull);
    });

    test('sin restricción: Todas las sedes', () {
      final u = ManagedUser.fromJson(_userJson());
      expect(clientLocationAccessLabel(u, names), 'Todas las sedes');
      expect(clientLocationAccessCompact(u), 'Todas las sedes');
    });

    test('restringido con nombres', () {
      expect(
        clientLocationAccessLabel(restricted(['b', 'a']), names),
        '2 sedes: Norte, Sur',
      );
      expect(
        clientLocationAccessLabel(restricted(['a']), names),
        '1 sede: Norte',
      );
    });

    test('recorta a maxNames con "y N más"', () {
      expect(
        clientLocationAccessLabel(restricted(['a', 'b', 'c', 'd']), names),
        '4 sedes: Centro, Este, Norte y 1 más',
      );
    });

    test('sin nombres cargados cae al conteo', () {
      expect(clientLocationAccessLabel(restricted(['a', 'b']), {}), '2 sedes');
      expect(clientLocationAccessLabel(restricted(['a']), {}), '1 sede');
    });

    test('compacta para la lista', () {
      expect(clientLocationAccessCompact(restricted(['a', 'b'])), '2 sedes');
      expect(clientLocationAccessCompact(restricted(['a'])), '1 sede');
    });
  });
}
