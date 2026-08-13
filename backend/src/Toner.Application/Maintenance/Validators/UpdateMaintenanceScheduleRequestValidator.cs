using FluentValidation;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance.Validators;

public class UpdateMaintenanceScheduleRequestValidator : AbstractValidator<UpdateMaintenanceScheduleRequest>
{
    public UpdateMaintenanceScheduleRequestValidator()
    {
        RuleFor(x => x.PrintThreshold).GreaterThan(0).When(x => x.PrintThreshold.HasValue);
        RuleFor(x => x.TimeIntervalDays).GreaterThan(0).When(x => x.TimeIntervalDays.HasValue);
    }
}
