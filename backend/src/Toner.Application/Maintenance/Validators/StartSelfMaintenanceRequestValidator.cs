using FluentValidation;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance.Validators;

public class StartSelfMaintenanceRequestValidator : AbstractValidator<StartSelfMaintenanceRequest>
{
    public StartSelfMaintenanceRequestValidator()
    {
        Include(new CreateManualMaintenanceOrderRequestValidator());

        RuleFor(x => x.PlannedAt!.Value)
            .Must(d => d.ToUniversalTime() > DateTime.UtcNow)
            .WithMessage("La fecha y hora programadas deben ser futuras.")
            .Must(d => d.ToUniversalTime() <= DateTime.UtcNow.AddDays(ScheduleMaintenanceOrderRequestValidator.MaxDaysAhead))
            .WithMessage($"No se puede programar con más de {ScheduleMaintenanceOrderRequestValidator.MaxDaysAhead} días de anticipación.")
            .OverridePropertyName(nameof(StartSelfMaintenanceRequest.PlannedAt))
            .When(x => x.PlannedAt.HasValue);
    }
}
