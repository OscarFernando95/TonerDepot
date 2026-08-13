namespace Toner.Domain.Common;

// Nombres canónicos de rol, usados al sembrar la tabla Roles y en [Authorize(Roles = ...)].
public static class RoleNames
{
    public const string Administrador = "Administrador";
    public const string Coordinador = "Coordinador";
    public const string Tecnico = "Tecnico";
    public const string Cliente = "Cliente";
    public const string Ventas = "Ventas";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Administrador, Coordinador, Tecnico, Cliente, Ventas
    };

    // Para [Authorize(Roles = RoleNames.StaffRoles)]: roles de back-office que administran clientes/activos/operación.
    public const string StaffRoles = Administrador + "," + Coordinador;

    // Para endpoints donde además del staff puede entrar el propio cliente a ver/crear sus datos (ej. tickets).
    public const string StaffAndClientRoles = StaffRoles + "," + Cliente;

    // Para endpoints donde además del staff puede entrar el propio técnico a ver lo suyo (ej. sus órdenes).
    public const string StaffAndTechnicianRoles = StaffRoles + "," + Tecnico;

    // Para lectura de tickets: Staff (todos), Cliente (los suyos), Tecnico (los suyos).
    public const string StaffClientAndTechnicianRoles = StaffAndClientRoles + "," + Tecnico;
}
