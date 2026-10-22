import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/maintenance_order.dart';
import 'package:toner_tecnico/models/maintenance_order_comment.dart';
import 'package:toner_tecnico/models/maintenance_thread_text.dart';
import 'package:toner_tecnico/services/maintenance_order_api.dart';
import 'package:toner_tecnico/services/realtime_service.dart';
import 'package:toner_tecnico/state/maintenance_order_thread_state.dart';

Map<String, dynamic> orderJson({
  String status = 'Asignada',
  Map<String, dynamic> extra = const {},
}) => {
  'id': 'o1',
  'maintenanceScheduleId': 's',
  'assetId': 'a',
  'assetBrandName': 'Ricoh',
  'assetModel': 'MP2553',
  'assetSerialNumber': 'X1',
  'status': status,
  'scheduledDate': '2026-10-01T00:00:00',
  'createdAt': '2026-10-01T10:00:00Z',
  ...extra,
};

Map<String, dynamic> commentJson({
  String id = 'c1',
  String kind = 'Comentario',
  String body = 'Hola',
  String? plannedAt,
  String role = 'Cliente',
  String name = 'Ana',
}) => {
  'id': id,
  'maintenanceOrderId': 'o1',
  'kind': kind,
  'body': body,
  'plannedAt': plannedAt,
  'authorUserId': 'u1',
  'authorName': name,
  'authorRole': role,
  'createdAt': '2026-10-20T15:00:00Z',
};

MaintenanceOrderComment comment({
  String kind = 'Comentario',
  String? plannedAt,
  String role = 'Cliente',
  String name = 'Ana',
}) => MaintenanceOrderComment.fromJson(
  commentJson(kind: kind, plannedAt: plannedAt, role: role, name: name),
);

class _FakeApi implements MaintenanceOrderApi {
  List<MaintenanceOrderComment> thread = [];
  Object? failWith;
  int listCalls = 0;

  @override
  Future<List<MaintenanceOrderComment>> listComments(String id) async {
    listCalls++;
    if (failWith != null) throw failWith!;
    return List.of(thread);
  }

  @override
  Future<MaintenanceOrderComment> addComment(String id, String body) async {
    if (failWith != null) throw failWith!;
    final created = MaintenanceOrderComment.fromJson(
      commentJson(id: 'new${thread.length}', body: body.trim()),
    );
    thread = [...thread, created];
    return created;
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  group('modelo de la orden', () {
    test('parsea plannedAt/lastCommentAt/commentCount', () {
      final o = MaintenanceOrder.fromJson(
        orderJson(
          extra: {
            'plannedAt': '2026-10-30T15:00:00Z',
            'lastCommentAt': '2026-10-22T10:00:00',
            'commentCount': 3,
          },
        ),
      );
      expect(o.plannedAt, DateTime.utc(2026, 10, 30, 15));
      expect(o.plannedAt!.isUtc, isTrue);
      // Sin 'Z' el servidor igual manda UTC.
      expect(o.lastCommentAt, DateTime.utc(2026, 10, 22, 10));
      expect(o.commentCount, 3);
    });

    test('un servidor anterior (sin los campos) o con null no rompe', () {
      final old = MaintenanceOrder.fromJson(orderJson());
      expect(old.plannedAt, isNull);
      expect(old.lastCommentAt, isNull);
      expect(old.commentCount, 0);
      final nulls = MaintenanceOrder.fromJson(
        orderJson(
          extra: {
            'plannedAt': null,
            'lastCommentAt': null,
            'commentCount': null,
          },
        ),
      );
      expect(nulls.plannedAt, isNull);
      expect(nulls.commentCount, 0);
    });

    test('solo se programa en Asignada; el hilo se cierra al terminar', () {
      expect(
        MaintenanceOrder.fromJson(orderJson()).canScheduleByTechnician,
        isTrue,
      );
      for (final s in ['Pendiente', 'EnProceso', 'Completada', 'Cancelada']) {
        expect(
          MaintenanceOrder.fromJson(orderJson(status: s))
              .canScheduleByTechnician,
          isFalse,
          reason: s,
        );
      }
      expect(
        MaintenanceOrder.fromJson(orderJson(status: 'EnProceso'))
            .isThreadReadOnly,
        isFalse,
      );
      expect(
        MaintenanceOrder.fromJson(orderJson(status: 'Completada'))
            .isThreadReadOnly,
        isTrue,
      );
      expect(
        MaintenanceOrder.fromJson(orderJson(status: 'Cancelada'))
            .isThreadReadOnly,
        isTrue,
      );
    });
  });

  group('modelo del comentario', () {
    test('parsea un comentario y una programación', () {
      final c = comment();
      expect(c.isSchedule, isFalse);
      expect(c.isFromClient, isTrue);
      expect(c.plannedAt, isNull);
      final p = comment(
        kind: 'Programacion',
        plannedAt: '2026-10-30T15:00:00Z',
        role: 'Tecnico',
        name: 'Juan',
      );
      expect(p.isSchedule, isTrue);
      expect(p.isFromClient, isFalse);
      expect(p.plannedAt, DateTime.utc(2026, 10, 30, 15));
    });
  });

  group('formato es-CO', () {
    test('día, fecha y hora de 24 h, y el año solo si es otro', () {
      final d = DateTime(2026, 10, 30, 10, 5); // viernes
      expect(
        formatDateTimeEsCo(d, now: DateTime(2026, 10, 22)),
        'vie 30 oct, 10:05',
      );
      expect(
        formatDateTimeEsCo(
          DateTime(2027, 1, 7, 8, 0),
          now: DateTime(2026, 10, 22),
        ),
        'jue 7 ene 2027, 08:00',
      );
      expect(
        formatDateTimeEsCo(
          DateTime(2026, 11, 4, 18, 30),
          now: DateTime(2026, 10, 22),
        ),
        'mié 4 nov, 18:30',
      );
    });

    test('convierte el instante UTC a hora local', () {
      final utc = DateTime.utc(2026, 10, 30, 15);
      final local = utc.toLocal();
      expect(
        formatDateTimeEsCo(utc, now: local),
        contains('${local.hour.toString().padLeft(2, '0')}:00'),
      );
    });
  });

  group('textos', () {
    test('roles legibles', () {
      expect(maintenanceRoleLabel('Tecnico'), 'Técnico');
      expect(maintenanceRoleLabel('Cliente'), 'Cliente');
      expect(maintenanceRoleLabel('Coordinador'), 'Coordinador');
      expect(maintenanceRoleLabel('Administrador'), 'Administrador');
      expect(maintenanceRoleLabel('Otro'), 'Otro');
    });

    test('evento de programación y de reprogramación', () {
      final planned = DateTime(2026, 10, 30, 10);
      final e = MaintenanceOrderComment.fromJson(
        commentJson(
          kind: 'Programacion',
          role: 'Tecnico',
          name: 'Juan',
          plannedAt: planned.toUtc().toIso8601String(),
        ),
      );
      final now = DateTime(2026, 10, 22);
      expect(
        scheduleEventText(e, now: now),
        'Juan programó el mantenimiento para el vie 30 oct, 10:00',
      );
      expect(
        scheduleEventText(e, isReschedule: true, now: now),
        'Juan reprogramó el mantenimiento para el vie 30 oct, 10:00',
      );
    });

    test(
      'rescheduleFlags: solo las programaciones posteriores a la primera',
      () {
        final s = comment(
          kind: 'Programacion',
          plannedAt: '2026-10-30T15:00:00Z',
          role: 'Tecnico',
        );
        final c = comment();
        expect(rescheduleFlags([s, c, s, s]), [false, false, true, true]);
        expect(rescheduleFlags([c, s]), [false, false]);
      },
    );

    test('botón de comentarios con conteo', () {
      expect(commentsButtonLabel(0), 'Comentarios');
      expect(commentsButtonLabel(4), 'Comentarios (4)');
    });

    test('línea de fecha del cliente', () {
      final now = DateTime(2026, 10, 22);
      final planned = DateTime(2026, 10, 30, 10).toUtc().toIso8601String();
      expect(
        clientPlannedLabel(
          MaintenanceOrder.fromJson(orderJson(extra: {'plannedAt': planned})),
          now: now,
        ),
        'Programado para vie 30 oct, 10:00',
      );
      expect(
        clientPlannedLabel(MaintenanceOrder.fromJson(orderJson())),
        'Aún sin fecha programada',
      );
      expect(
        clientPlannedLabel(
          MaintenanceOrder.fromJson(orderJson(status: 'Cancelada')),
        ),
        isNull,
      );
      expect(
        clientPlannedLabel(
          MaintenanceOrder.fromJson(orderJson(status: 'Completada')),
        ),
        isNull,
      );
    });

    test('Programar / Reprogramar y fecha para el técnico', () {
      final sin = MaintenanceOrder.fromJson(orderJson());
      final con = MaintenanceOrder.fromJson(
        orderJson(extra: {'plannedAt': '2026-10-30T15:00:00Z'}),
      );
      expect(scheduleButtonLabel(sin), 'Programar');
      expect(scheduleButtonLabel(con), 'Reprogramar');
      expect(plannedLabelForStaff(sin), 'Sin fecha programada');
    });
  });

  group('validación', () {
    test('comentario vacío, solo espacios, límite exacto y excedido', () {
      expect(validateComment(''), isNotNull);
      expect(validateComment('   \n '), isNotNull);
      expect(validateComment('Ese día no puedo'), isNull);
      expect(validateComment('a' * 1000), isNull);
      expect(validateComment('a' * 1001), isNotNull);
      // Se mide recortado, como el servidor.
      expect(validateComment('  ${'a' * 1000}  '), isNull);
      expect(canSendComment(''), isFalse);
      expect(canSendComment('hola'), isTrue);
      expect(commentCounterText('hola'), '4/1000');
    });

    test('fecha de programación: futura y a <= 365 días', () {
      final now = DateTime(2026, 10, 22, 12);
      expect(
        validatePlannedAt(DateTime(2026, 10, 22, 11), now: now),
        isNotNull,
      );
      expect(validatePlannedAt(now, now: now), isNotNull);
      expect(validatePlannedAt(DateTime(2026, 10, 22, 13), now: now), isNull);
      expect(
        validatePlannedAt(now.add(const Duration(days: 365)), now: now),
        isNull,
      );
      expect(
        validatePlannedAt(now.add(const Duration(days: 366)), now: now),
        isNotNull,
      );
    });

    test('combina día y hora, y envía ISO UTC', () {
      final d = combineDateAndTime(DateTime(2026, 10, 30, 23, 59), 10, 30);
      expect(d, DateTime(2026, 10, 30, 10, 30));
      final iso = plannedAtToUtcIso(d);
      expect(iso.endsWith('Z'), isTrue);
      expect(DateTime.parse(iso), d.toUtc());
    });
  });

  group('MaintenanceOrderThreadState', () {
    late _FakeApi api;
    EntityChangedHandler? realtimeHandler;
    var unsubscribed = false;

    MaintenanceOrderThreadState build() {
      realtimeHandler = null;
      unsubscribed = false;
      return MaintenanceOrderThreadState(
        api,
        'o1',
        subscribe: (entities, handler) {
          expect(entities, ['MaintenanceOrder']);
          realtimeHandler = handler;
          return () => unsubscribed = true;
        },
      );
    }

    final originalDebugPrint = debugPrint;

    setUp(() {
      api = _FakeApi();
      debugPrint = (String? message, {int? wrapWidth}) {};
    });

    tearDown(() => debugPrint = originalDebugPrint);

    test('carga el hilo', () async {
      api.thread = [comment()];
      final s = build();
      await s.load();
      expect(s.comments, hasLength(1));
      expect(s.loading, isFalse);
      expect(s.loadError, isNull);
      s.dispose();
      expect(unsubscribed, isTrue);
    });

    test(
      'error de carga: mensaje al usuario; la recarga silenciosa no lo pisa',
      () async {
        api.failWith = Exception('boom');
        final s = build();
        await s.load();
        expect(s.loadError, 'No se pudieron cargar los comentarios.');
        api.failWith = null;
        api.thread = [comment()];
        await s.load(silent: true);
        expect(s.comments, hasLength(1));
        expect(s.loadError, isNull);
        s.dispose();
      },
    );

    test('enviar: valida, publica y no duplica si el realtime ya lo trajo', () async {
      final s = build();
      expect(await s.send('   '), isFalse);
      expect(s.sendError, isNotNull);
      expect(s.comments, isEmpty);

      expect(await s.send('  Ese día no puedo  '), isTrue);
      expect(s.sendError, isNull);
      expect(s.comments.single.body, 'Ese día no puedo');

      // El aviso en tiempo real recarga el hilo completo: queda una sola copia.
      await s.load(silent: true);
      expect(s.comments, hasLength(1));
      s.dispose();
    });

    test(
      'enviar con error del servidor deja el mensaje y conserva el hilo',
      () async {
        api.thread = [comment()];
        final s = build();
        await s.load();
        api.failWith = Exception('409');
        expect(await s.send('hola'), isFalse);
        expect(s.sendError, 'No se pudo enviar el comentario.');
        expect(s.comments, hasLength(1));
        expect(s.sending, isFalse);
        s.dispose();
      },
    );

    test('el realtime recarga solo si toca esta orden (o es resync)', () async {
      final s = build();
      await s.load();
      final base = api.listCalls;
      realtimeHandler!(
        const EntityChangedEvent(
          entity: 'MaintenanceOrder',
          id: 'otra',
          action: 'updated',
        ),
      );
      await Future<void>.delayed(Duration.zero);
      expect(api.listCalls, base);
      realtimeHandler!(
        const EntityChangedEvent(
          entity: 'MaintenanceOrder',
          id: 'O1',
          action: 'updated',
        ),
      );
      await Future<void>.delayed(Duration.zero);
      expect(api.listCalls, base + 1);
      realtimeHandler!(EntityChangedEvent.resync);
      await Future<void>.delayed(Duration.zero);
      expect(api.listCalls, base + 2);
      s.dispose();
    });
  });
}
