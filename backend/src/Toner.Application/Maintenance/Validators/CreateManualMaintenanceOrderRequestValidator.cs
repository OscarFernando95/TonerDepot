using FluentValidation;
using Toner.Application.Maintenance.Dtos;

namespace Toner.Application.Maintenance.Validators;

public class CreateManualMaintenanceOrderRequestValidator : AbstractValidator<CreateManualMaintenanceOrderRequest>
{
    public CreateManualMaintenanceOrderRequestValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();

        RuleFor(x => x)
            .Must(x => x.IncludesGeneral || x.IncludesUnits || x.IncludesConsumables)
            .WithName(nameof(CreateManualMaintenanceOrderRequest.IncludesGeneral))
            .WithMessage("Selecciona al menos un tipo de mantenimiento: general, unidades o insumos.");

        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
