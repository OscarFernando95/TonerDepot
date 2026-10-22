using FluentValidation;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance.Validators;

public class ScheduleMaintenanceOrderRequestValidator : AbstractValidator<ScheduleMaintenanceOrderRequest>
{
    // Más allá de un año no es una programación sino un error de captura.
    public const int MaxDaysAhead = 365;

    public ScheduleMaintenanceOrderRequestValidator()
    {
        RuleFor(x => x.PlannedAt)
            .Must(d => d.ToUniversalTime() > DateTime.UtcNow)
            .WithMessage("La fecha y hora programadas deben ser futuras.")
            .Must(d => d.ToUniversalTime() <= DateTime.UtcNow.AddDays(MaxDaysAhead))
            .WithMessage($"No se puede programar con más de {MaxDaysAhead} días de anticipación.");
    }
}
