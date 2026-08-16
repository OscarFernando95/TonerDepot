using FluentValidation;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance.Validators;

public class CompleteMaintenanceOrderRequestValidator : AbstractValidator<CompleteMaintenanceOrderRequest>
{
    public CompleteMaintenanceOrderRequestValidator()
    {
        RuleFor(x => x.CounterValue).GreaterThanOrEqualTo(0);
    }
}
