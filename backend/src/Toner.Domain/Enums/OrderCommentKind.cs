namespace Toner.Domain.Enums;

public enum OrderCommentKind
{
    // Texto libre de quien participa en la orden (cliente, técnico o staff).
    Comentario = 0,

    // Registro de que el técnico programó (o reprogramó) la fecha y hora; PlannedAt trae la fecha.
    Programacion = 1
}
