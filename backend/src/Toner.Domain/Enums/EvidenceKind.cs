namespace Toner.Domain.Enums;

public enum EvidenceKind
{
    // Foto de la falla o del estado del equipo al llegar (check-in).
    Antes = 0,
    // Foto del resultado al terminar (check-out).
    Despues = 1,
    // Foto del contador del equipo al cerrar la visita: respalda la lectura registrada en el check-out.
    Contador = 2
}
