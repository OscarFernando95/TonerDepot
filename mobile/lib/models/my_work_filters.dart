import 'maintenance_order.dart';
import 'service_ticket.dart';

/// Lógica pura de "Mi trabajo": qué ve el técnico en cada sección. Solo lo PENDIENTE (con check-in posible):
/// lo resuelto, cerrado, cancelado o completado desaparece de la lista apenas se termina.

/// Tickets propios pendientes: Asignado o EnProceso.
List<ServiceTicket> pendingTickets(Iterable<ServiceTicket> tickets) => [
  for (final t in tickets)
    if (t.status == 'Asignado' || t.status == 'EnProceso') t,
];

/// Órdenes propias pendientes: Asignada o EnProceso.
List<MaintenanceOrder> pendingOrders(Iterable<MaintenanceOrder> orders) => [
  for (final o in orders)
    if (o.status == 'Asignada' || o.status == 'EnProceso') o,
];

/// Quién tiene un ticket de las máquinas del técnico: 'Sin asignar' o el nombre del técnico.
String ticketAssigneeLabel(ServiceTicket ticket) {
  final name = ticket.technicianName?.trim();
  return (name == null || name.isEmpty) ? 'Sin asignar' : name;
}
