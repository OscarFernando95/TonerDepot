import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/services/realtime_service.dart';

EntityChangedEvent ev(String entity, String id, [String action = 'updated']) =>
    EntityChangedEvent(entity: entity, id: id, action: action);

void main() {
  const wait = Duration(milliseconds: 60);
  late RealtimeDispatcher d;
  setUp(() => d = RealtimeDispatcher(debounce: const Duration(milliseconds: 20)));

  test('filtra por entidad', () async {
    final got = <String>[];
    d.subscribe(['Ticket'], (e) => got.add(e.entity));
    d.dispatch(ev('Asset', '1'));
    d.dispatch(ev('Ticket', '1'));
    await Future<void>.delayed(wait);
    expect(got, ['Ticket']);
  });

  test('agrupa ráfagas del mismo entity:id, pero no ids distintos', () async {
    final got = <String>[];
    d.subscribe(['Ticket'], (e) => got.add(e.id));
    for (var i = 0; i < 5; i++) {
      d.dispatch(ev('Ticket', 'a'));
    }
    d.dispatch(ev('Ticket', 'b'));
    await Future<void>.delayed(wait);
    expect(got..sort(), ['a', 'b']);
  });

  test('resync llega a todos los handlers sin importar el filtro', () async {
    var a = 0, b = 0;
    d.subscribe(['Ticket'], (_) => a++);
    d.subscribe(['Holiday'], (e) {
      expect(e.isResync, isTrue);
      b++;
    });
    d.dispatch(EntityChangedEvent.resync);
    await Future<void>.delayed(wait);
    expect([a, b], [1, 1]);
  });

  test('un resync no se funde con un evento normal', () async {
    final got = <String>[];
    d.subscribe(['Ticket'], (e) => got.add(e.action));
    d.dispatch(ev('Ticket', ''));
    d.dispatch(EntityChangedEvent.resync);
    await Future<void>.delayed(wait);
    expect(got.length, 2);
  });

  test('cancelar la suscripción descarta eventos pendientes', () async {
    var n = 0;
    final off = d.subscribe(['Ticket'], (_) => n++);
    d.dispatch(ev('Ticket', '1'));
    off();
    await Future<void>.delayed(wait);
    expect(n, 0);
    expect(d.subscriberCount, 0);
  });

  test('un handler que lanza no impide los demás', () async {
    var ok = 0;
    d.subscribe(['Ticket'], (_) => throw StateError('x'));
    d.subscribe(['Ticket'], (_) => ok++);
    d.dispatch(ev('Ticket', '1'));
    await Future<void>.delayed(wait);
    expect(ok, 1);
  });

  test('affects: coincide por id (sin mayúsculas) o resync', () {
    expect(ev('Ticket', 'ABC').affects('abc'), isTrue);
    expect(ev('Ticket', 'x').affects('abc'), isFalse);
    expect(EntityChangedEvent.resync.affects('abc'), isTrue);
  });
}
