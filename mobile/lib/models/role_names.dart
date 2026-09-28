/// Mirror de backend/src/Toner.Domain/Common/RoleNames.cs — mismos strings,
/// mismas combinaciones. El backend no tiene permisos granulares, solo estos
/// 5 roles fijos, así que esto es toda la fuente de verdad que necesita el
/// cliente para decidir qué mostrar.
class RoleNames {
  RoleNames._();

  static const administrador = 'Administrador';
  static const coordinador = 'Coordinador';
  static const tecnico = 'Tecnico';
  static const cliente = 'Cliente';
  static const ventas = 'Ventas';

  static const all = [administrador, coordinador, tecnico, cliente, ventas];
  static const staffRoles = [administrador, coordinador];
  static const staffAndClientRoles = [administrador, coordinador, cliente];
  static const staffAndTechnicianRoles = [administrador, coordinador, tecnico];
  static const staffClientAndTechnicianRoles = [
    administrador,
    coordinador,
    cliente,
    tecnico,
  ];
}
