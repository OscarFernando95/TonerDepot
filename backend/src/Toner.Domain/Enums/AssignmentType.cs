namespace Toner.Domain.Enums;

public enum AssignmentType
{
    Automatica = 0,
    Manual = 1,
    // El propio técnico se autoasigna un ticket/orden que estaba libre o asignado a otro técnico que
    // aún no lo había iniciado (ver ClaimAsync en MaintenanceOrderService/ServiceTicketService).
    Reclamada = 2
}
