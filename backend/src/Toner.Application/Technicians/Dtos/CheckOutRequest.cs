namespace Toner.Application.Technicians.Dtos;

public class CheckOutRequest
{
    // true (default): el trabajo quedó terminado, se marca Resuelto/Completada.
    // false: el técnico solo se libera (pausa, fin de turno, etc.), el ticket/orden sigue EnProceso.
    public bool Resolved { get; set; } = true;

    public string? Notes { get; set; }
}
