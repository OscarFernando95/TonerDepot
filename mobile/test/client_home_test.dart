import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:toner_tecnico/models/client_home.dart';
import 'package:toner_tecnico/models/client_home_text.dart';
import 'package:toner_tecnico/screens/portal/client_home_cards.dart';

ClientHome _home({
  int openTickets = 0,
  int ticketsInProgress = 0,
  int openMaintenance = 0,
  int scheduled = 0,
  int inProgress = 0,
  int withoutDate = 0,
  int pendingInstallations = 0,
  int installationsInProgress = 0,
  int installed = 0,
  int inMaintenance = 0,
  int withTicket = 0,
  List<ClientHomeTicket> tickets = const [],
  List<ClientHomeMaintenance> maintenance = const [],
  List<ClientHomeInstallation> installations = const [],
}) => ClientHome(
  clientName: 'Acme SAS',
  openTickets: openTickets,
  ticketsInProgress: ticketsInProgress,
  openMaintenance: openMaintenance,
  maintenanceScheduled: scheduled,
  maintenanceInProgress: inProgress,
  maintenanceWithoutVisitDate: withoutDate,
  pendingInstallations: pendingInstallations,
  installationsInProgress: installationsInProgress,
  installedAssets: installed,
  assetsInMaintenance: inMaintenance,
  assetsWithOpenTicket: withTicket,
  recentTickets: tickets,
  upcomingMaintenance: maintenance,
  installations: installations,
);

ClientHomeTicket _ticket(
  String id, {
  String status = 'Abierto',
  String priority = 'Media',
  String? tech,
  DateTime? createdAt,
}) => ClientHomeTicket(
  id: id,
  equipment: 'Ricoh MP 301',
  locationName: 'Sede Norte',
  description: 'No imprime',
  status: status,
  priority: priority,
  technicianName: tech,
  createdAt: createdAt,
);

ClientHomeMaintenance _visit(
  String id, {
  String status = 'Asignada',
  DateTime? plannedAt,
  int comments = 0,
}) => ClientHomeMaintenance(
  id: id,
  equipment: 'Ricoh MP 301',
  locationName: 'Sede Norte',
  status: status,
  plannedAt: plannedAt,
  commentCount: comments,
);

void main() {
  group('ClientHome.fromJson', () {
    test('parsea la respuesta completa', () {
      final home = ClientHome.fromJson({
        'clientName': 'Acme SAS',
        'locationCount': 2,
        'openTickets': 3,
        'ticketsInProgress': 1,
        'openMaintenance': 4,
        'maintenanceScheduled': 2,
        'maintenanceInProgress': 1,
        'maintenanceWithoutVisitDate': 1,
        'pendingInstallations': 2,
        'installationsInProgress': 1,
        'installedAssets': 10,
        'assetsInMaintenance': 1,
        'assetsWithOpenTicket': 3,
        'recentTickets': [
          {
            'id': 't1',
            'equipment': 'Ricoh MP 301',
            'locationName': 'Sede Norte',
            'description': 'No imprime',
            'status': 'EnProceso',
            'priority': 'Critica',
            'technicianName': 'Luis',
            'createdAt': '2026-10-22T10:00:00Z',
          },
        ],
        'upcomingMaintenance': [
          {
            'id': 'm1',
            'equipment': 'HP M404',
            'locationName': 'Sede Sur',
            'status': 'Asignada',
            'scheduledDate': '2026-11-05T00:00:00',
            'plannedAt': '2026-11-03T15:00:00Z',
            'technicianName': 'Ana',
            'commentCount': 2,
          },
        ],
        'installations': [
          {
            'assetId': 'a1',
            'equipment': 'Canon X',
            'locationName': 'Sede Sur',
            'inProgress': true,
          },
        ],
      });
      expect(home.clientName, 'Acme SAS');
      expect(home.openTickets, 3);
      expect(home.recentTickets.single.priority, 'Critica');
      expect(
        home.recentTickets.single.createdAt,
        DateTime.utc(2026, 10, 22, 10),
      );
      expect(
        home.upcomingMaintenance.single.plannedAt,
        DateTime.utc(2026, 11, 3, 15),
      );
      expect(home.upcomingMaintenance.single.commentCount, 2);
      expect(home.installations.single.inProgress, isTrue);
    });

    test('tolera nulos, tipos raros y elementos corruptos', () {
      final home = ClientHome.fromJson({
        'clientName': null,
        'openTickets': null,
        'ticketsInProgress': '3',
        'recentTickets': [
          {'id': 't1', 'status': null, 'createdAt': 'no-es-fecha'},
          'basura',
          null,
        ],
        'upcomingMaintenance': 'no-es-lista',
        'installations': null,
      });
      expect(home.clientName, '');
      expect(home.openTickets, 0);
      expect(home.ticketsInProgress, 0);
      expect(home.recentTickets, hasLength(1));
      expect(home.recentTickets.single.status, '');
      expect(home.recentTickets.single.priority, 'Media');
      expect(home.recentTickets.single.createdAt, isNull);
      expect(home.upcomingMaintenance, isEmpty);
      expect(home.installations, isEmpty);
    });

    test('un JSON vacío da un inicio en cero', () {
      final home = ClientHome.fromJson({});
      expect(home.openTickets + home.openMaintenance + home.installedAssets, 0);
    });
  });

  group('saludo', () {
    test('usa la empresa y si falta, el nombre del usuario', () {
      expect(greetingName('  Acme SAS ', 'Ana'), 'Acme SAS');
      expect(greetingName('', 'Ana Pérez'), 'Ana Pérez');
      expect(greetingName('', null), '');
    });
  });

  group('tickets', () {
    test('sin abiertos: estado vacío cálido y sin filas', () {
      final home = _home(tickets: [_ticket('t1', status: 'Cerrado')]);
      expect(
        ticketsSubtitle(home),
        'Todo en orden: no tienes tickets abiertos',
      );
      expect(ticketsAccent(home), HomeAccent.calm);
      expect(visibleTickets(home), isEmpty);
    });

    test('con abiertos: frase de en curso y concordancia singular/plural', () {
      expect(ticketsCountLabel(1), 'ticket abierto');
      expect(ticketsCountLabel(3), 'tickets abiertos');
      final home = _home(
        openTickets: 3,
        ticketsInProgress: 2,
        tickets: [_ticket('1'), _ticket('2'), _ticket('3')],
      );
      expect(ticketsSubtitle(home), startsWith('2 en curso'));
      expect(
        ticketsSubtitle(_home(openTickets: 1)),
        'Ninguno en curso todavía',
      );
    });

    test('muestra hasta 3 filas y cuenta las que quedan fuera', () {
      final home = _home(
        openTickets: 5,
        tickets: [for (var i = 0; i < 5; i++) _ticket('t$i')],
      );
      expect(visibleTickets(home), hasLength(3));
      expect(hiddenTicketCount(home), 2);
      expect(
        hiddenTicketCount(
          _home(openTickets: 2, tickets: [_ticket('a'), _ticket('b')]),
        ),
        0,
      );
    });

    test(
      'acento: ámbar con abiertos, rojo solo si hay uno Crítico abierto',
      () {
        expect(
          ticketsAccent(
            _home(openTickets: 1, tickets: [_ticket('a', priority: 'Alta')]),
          ),
          HomeAccent.attention,
        );
        expect(
          ticketsAccent(
            _home(openTickets: 1, tickets: [_ticket('a', priority: 'Critica')]),
          ),
          HomeAccent.urgent,
        );
        // Un Crítico ya cerrado no pinta de rojo el inicio.
        expect(
          ticketsAccent(
            _home(
              openTickets: 1,
              tickets: [
                _ticket('a', priority: 'Critica', status: 'Cerrado'),
                _ticket('b'),
              ],
            ),
          ),
          HomeAccent.attention,
        );
      },
    );

    test('técnico: nombre, "esperando técnico" o nada si ya terminó', () {
      expect(ticketTechnicianLabel(_ticket('a', tech: 'Luis')), 'Atiende Luis');
      expect(ticketTechnicianLabel(_ticket('a')), 'Esperando técnico');
      expect(ticketTechnicianLabel(_ticket('a', status: 'Cerrado')), isNull);
    });

    test('criticidad con tilde', () {
      expect(priorityLabelEs('Critica'), 'Crítica');
      expect(priorityLabelEs('Alta'), 'Alta');
    });
  });

  group('timeAgoEs', () {
    final now = DateTime.utc(2026, 10, 22, 12);
    test('escala de minutos a semanas', () {
      expect(
        timeAgoEs(now.subtract(const Duration(seconds: 20)), now: now),
        'hace un momento',
      );
      expect(
        timeAgoEs(now.subtract(const Duration(minutes: 5)), now: now),
        'hace 5 min',
      );
      expect(
        timeAgoEs(now.subtract(const Duration(hours: 3)), now: now),
        'hace 3 h',
      );
      expect(
        timeAgoEs(now.subtract(const Duration(days: 1)), now: now),
        'hace 1 día',
      );
      expect(
        timeAgoEs(now.subtract(const Duration(days: 4)), now: now),
        'hace 4 días',
      );
      expect(
        timeAgoEs(now.subtract(const Duration(days: 15)), now: now),
        'hace 2 sem',
      );
    });
    test('fecha futura (reloj desfasado) y nula no rompen', () {
      expect(
        timeAgoEs(now.add(const Duration(minutes: 3)), now: now),
        'hace un momento',
      );
      expect(timeAgoEs(null, now: now), '');
    });
    test('pasados dos meses, mes y año', () {
      expect(timeAgoEs(DateTime.utc(2026, 5, 15, 12), now: now), 'may 2026');
    });
  });

  group('mantenimientos', () {
    test('desglose: solo lo que tiene algo', () {
      final home = _home(
        openMaintenance: 4,
        scheduled: 2,
        inProgress: 1,
        withoutDate: 1,
      );
      expect(maintenanceBreakdown(home), [
        '2 programados',
        '1 en curso',
        '1 esperando fecha',
      ]);
      expect(maintenanceBreakdown(_home(openMaintenance: 1, scheduled: 1)), [
        '1 programado',
      ]);
      expect(maintenanceBreakdown(_home()), isEmpty);
    });

    test('acento: gris sin nada, azul con visita, ámbar si espera fecha', () {
      expect(maintenanceAccent(_home()), HomeAccent.idle);
      expect(
        maintenanceAccent(_home(openMaintenance: 1, scheduled: 1)),
        HomeAccent.calm,
      );
      expect(
        maintenanceAccent(
          _home(openMaintenance: 2, scheduled: 1, withoutDate: 1),
        ),
        HomeAccent.attention,
      );
    });

    test('frase: sin nada y todo esperando fecha', () {
      expect(
        maintenanceSubtitle(_home()),
        'Sin mantenimientos programados por ahora',
      );
      expect(
        maintenanceSubtitle(_home(openMaintenance: 2, withoutDate: 2)),
        contains('confirmar la fecha'),
      );
    });

    test('etiqueta de visita: fecha local, por confirmar o en curso', () {
      // Se construye en hora local para que la prueba no dependa de la zona del equipo.
      final local = DateTime(2026, 10, 30, 10, 0);
      final now = DateTime(2026, 10, 22, 9);
      expect(
        visitLabel(_visit('m', plannedAt: local.toUtc()), now: now),
        'Visita: ${_expectedDate(local)}',
      );
      expect(visitLabel(_visit('m'), now: now), 'Fecha por confirmar');
      expect(
        visitLabel(_visit('m', status: 'EnProceso'), now: now),
        'En curso ahora',
      );
    });

    test('muestra la próxima y hasta dos más', () {
      final home = _home(
        openMaintenance: 5,
        maintenance: [for (var i = 0; i < 5; i++) _visit('m$i')],
      );
      expect(visibleMaintenance(home).map((m) => m.id), ['m0', 'm1', 'm2']);
    });

    test('marca de comentarios', () {
      expect(commentsHint(_visit('m')), isNull);
      expect(commentsHint(_visit('m', comments: 1)), '1 comentario');
      expect(commentsHint(_visit('m', comments: 4)), '4 comentarios');
    });
  });

  group('instalaciones', () {
    test('sin pendientes: tarjeta compacta e inactiva', () {
      final home = _home();
      expect(installationsAccent(home), HomeAccent.idle);
      expect(installationsSubtitle(home), 'Sin instalaciones pendientes');
    });

    test('con pendientes: cuántas se están instalando ahora', () {
      expect(
        installationsSubtitle(
          _home(pendingInstallations: 2, installationsInProgress: 1),
        ),
        '1 se está instalando ahora',
      );
      expect(
        installationsSubtitle(
          _home(pendingInstallations: 3, installationsInProgress: 2),
        ),
        '2 se están instalando ahora',
      );
      expect(installationsCountLabel(1), 'instalación pendiente');
      expect(installationsCountLabel(2), 'instalaciones pendientes');
    });
  });

  group('mis máquinas', () {
    test('en orden = instaladas - con ticket abierto', () {
      final h = machineHealth(
        _home(installed: 10, withTicket: 3, inMaintenance: 1),
      );
      expect(h.ok, 7);
      expect(h.withTicket, 3);
      expect(h.inMaintenance, 1);
      expect(h.total, 11);
      expect(h.attention, 4);
    });

    test('un ticket sobre más máquinas que las instaladas se recorta (nunca negativo)', () {
      final h = machineHealth(_home(installed: 2, withTicket: 5));
      expect(h.ok, 0);
      expect(h.withTicket, 2);
      final neg = machineHealth(
        _home(installed: -1, withTicket: 0, inMaintenance: -2),
      );
      expect(neg.total, 0);
    });

    test('frases: sin máquinas, todo en orden y con atención', () {
      expect(machinesSubtitle(_home()), 'Aún no tienes máquinas instaladas');
      expect(machinesSubtitle(_home(installed: 1)), 'Tu máquina está en orden');
      expect(
        machinesSubtitle(_home(installed: 4)),
        'Todas tus máquinas están en orden',
      );
      expect(
        machinesSubtitle(_home(installed: 4, withTicket: 1)),
        '1 de 4 necesita atención',
      );
      expect(
        machinesSubtitle(_home(installed: 4, withTicket: 2, inMaintenance: 1)),
        '3 de 5 necesitan atención',
      );
    });

    test('acento y chips', () {
      expect(machinesAccent(_home()), HomeAccent.idle);
      expect(machinesAccent(_home(installed: 3)), HomeAccent.calm);
      expect(
        machinesAccent(_home(installed: 3, withTicket: 1)),
        HomeAccent.attention,
      );
      final chips = machinesChips(
        _home(installed: 5, withTicket: 2, inMaintenance: 1),
      );
      expect(chips.map((c) => c.kind), ['ok', 'ticket', 'maintenance']);
      expect(chips.map((c) => c.label), [
        '3 en orden',
        '2 con tickets abiertos',
        '1 en mantenimiento',
      ]);
      expect(machinesChips(_home(installed: 2)).single.label, '2 en orden');
    });
  });

  group('semántica y lámpara', () {
    test('las lecturas de pantalla dicen el estado con palabras', () {
      final home = _home(
        openTickets: 2,
        ticketsInProgress: 1,
        tickets: [_ticket('a')],
      );
      expect(ticketsSemantics(home), contains('2 tickets abiertos'));
      expect(
        machinesSemantics(_home(installed: 3, withTicket: 1)),
        contains('1 de 3 necesita atención'),
      );
      expect(
        installationsSemantics(_home()),
        contains('Sin instalaciones pendientes'),
      );
    });

    test('la lámpara siempre tiene texto salvo cuando no hay nada', () {
      expect(accentLabel(HomeAccent.idle), '');
      expect(accentLabel(HomeAccent.calm), isNotEmpty);
      expect(accentLabel(HomeAccent.attention), isNotEmpty);
      expect(accentLabel(HomeAccent.urgent), isNotEmpty);
    });
  });

  group('tarjetas (widgets)', () {
    final full = _home(
      openTickets: 4,
      ticketsInProgress: 1,
      openMaintenance: 3,
      scheduled: 1,
      inProgress: 1,
      withoutDate: 1,
      pendingInstallations: 2,
      installationsInProgress: 1,
      installed: 6,
      inMaintenance: 1,
      withTicket: 2,
      tickets: [
        _ticket(
          't1',
          status: 'EnProceso',
          priority: 'Critica',
          tech: 'Luis',
          createdAt: DateTime.now().toUtc(),
        ),
        _ticket('t2'),
        _ticket('t3', status: 'Asignado', tech: 'Ana'),
      ],
      maintenance: [
        _visit(
          'm1',
          plannedAt: DateTime.now().toUtc().add(const Duration(days: 2)),
          comments: 2,
        ),
        _visit('m2'),
        _visit('m3', status: 'EnProceso'),
      ],
      installations: [
        const ClientHomeInstallation(
          assetId: 'a1',
          equipment: 'Canon X',
          locationName: 'Sede Sur',
          inProgress: true,
        ),
        const ClientHomeInstallation(assetId: 'a2', equipment: 'Canon Y'),
      ],
    );

    Widget host(Widget child, double width, {double textScale = 1}) =>
        MaterialApp(
          home: MediaQuery(
            data: MediaQueryData(
              size: Size(width, 900),
              textScaler: TextScaler.linear(textScale),
            ),
            child: Scaffold(
              body: SingleChildScrollView(
                padding: const EdgeInsets.all(16),
                child: child,
              ),
            ),
          ),
        );

    Widget cards(
      ClientHome home, {
      VoidCallback? onTap,
      ValueChanged<String>? onTicket,
    }) => Column(
      children: [
        TicketsCard(
          home: home,
          onOpenList: onTap ?? () {},
          onOpenTicket: onTicket ?? (_) {},
        ),
        MaintenanceCard(home: home, onOpen: onTap ?? () {}),
        InstallationsCard(home: home, onOpen: onTap ?? () {}),
        MachinesCard(home: home, onOpen: onTap ?? () {}),
      ],
    );

    testWidgets('se pintan sin desbordes en 320 dp con texto grande', (
      tester,
    ) async {
      tester.view.physicalSize = const Size(320, 2400);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(host(cards(full), 320, textScale: 1.6));
      await tester.pump();
      expect(tester.takeException(), isNull);
      expect(find.text('Criticidad Crítica'.toUpperCase()), findsOneWidget);
      expect(find.text('Instalando ahora'.toUpperCase()), findsOneWidget);
      expect(find.textContaining('Visita:'), findsWidgets);
    });

    testWidgets(
      'en pantalla ancha las tarjetas van emparejadas sin desbordes',
      (tester) async {
        tester.view.physicalSize = const Size(900, 1600);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.reset);
        Widget pair(Widget a, Widget b) => IntrinsicHeight(
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Expanded(child: a),
              const SizedBox(width: 14),
              Expanded(child: b),
            ],
          ),
        );
        await tester.pumpWidget(
          host(
            Column(
              children: [
                pair(
                  TicketsCard(
                    home: full,
                    onOpenList: () {},
                    onOpenTicket: (_) {},
                  ),
                  MaintenanceCard(home: full, onOpen: () {}),
                ),
                pair(
                  InstallationsCard(home: full, onOpen: () {}),
                  MachinesCard(home: full, onOpen: () {}),
                ),
              ],
            ),
            900,
          ),
        );
        await tester.pump();
        expect(tester.takeException(), isNull);
      },
    );

    testWidgets('estado vacío de tickets e instalaciones compacta', (
      tester,
    ) async {
      await tester.pumpWidget(host(cards(_home(installed: 2)), 400));
      await tester.pump();
      expect(tester.takeException(), isNull);
      expect(
        find.text('Todo en orden: no tienes tickets abiertos'),
        findsOneWidget,
      );
      expect(find.text('Sin instalaciones pendientes'), findsOneWidget);
    });

    testWidgets(
      'tocar una fila abre ese ticket y tocar la tarjeta abre la lista',
      (tester) async {
        var list = 0;
        String? opened;
        await tester.pumpWidget(
          host(
            TicketsCard(
              home: full,
              onOpenList: () => list++,
              onOpenTicket: (id) => opened = id,
            ),
            400,
          ),
        );
        await tester.tap(find.text('Ver todos los tickets (+1)'));
        await tester.pump();
        expect(list, 1);
        expect(opened, isNull);

        await tester.tap(find.textContaining('Atiende Luis'));
        await tester.pump();
        expect(opened, 't1');
        expect(list, 1);
      },
    );

    testWidgets('el esqueleto y el error se pintan; Reintentar avisa', (
      tester,
    ) async {
      await tester.pumpWidget(host(const HomeSkeleton(), 400));
      await tester.pump(const Duration(milliseconds: 300));
      expect(tester.takeException(), isNull);
      var retried = 0;
      await tester.pumpWidget(
        host(
          HomeErrorCard(message: 'Sin conexión', onRetry: () => retried++),
          400,
        ),
      );
      await tester.tap(find.text('Reintentar'));
      expect(retried, 1);
      expect(find.text('Sin conexión'), findsOneWidget);
    });
  });
}

/// Mismo formato que formatDateTimeEsCo, escrito a mano para no repetir la implementación en la prueba.
String _expectedDate(DateTime local) {
  const wd = ['lun', 'mar', 'mié', 'jue', 'vie', 'sáb', 'dom'];
  const mo = [
    'ene',
    'feb',
    'mar',
    'abr',
    'may',
    'jun',
    'jul',
    'ago',
    'sep',
    'oct',
    'nov',
    'dic',
  ];
  return '${wd[local.weekday - 1]} ${local.day} ${mo[local.month - 1]}, 10:00';
}
