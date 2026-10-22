namespace Toner.Application.Maintenance.Dtos;

// El técnico inicia por su cuenta un mantenimiento a demanda. Igual que el pedido del staff, y con la posibilidad de dejarlo
// PROGRAMADO para otro momento en vez de empezarlo ahora (el cliente ve la fecha y puede pedir un cambio en el hilo).
public class StartSelfMaintenanceRequest : CreateManualMaintenanceOrderRequest
{
    // UTC. null: lo va a hacer ahora (se exige estar en horario y sin otra visita en curso). Con valor: queda programado,
    // futuro y a no más de un año, y no se exige estar libre ahora.
    public DateTime? PlannedAt { get; set; }
}
