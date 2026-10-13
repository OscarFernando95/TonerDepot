using FluentValidation;
using Toner.Application.Technicians.Dtos;

namespace Toner.Application.Technicians.Validators;

// Los máximos de texto salen de las columnas reales: TimeLog.Notes varchar(1000), Asset.Area
// varchar(150), ServiceTicket.ExternalAssetBrand varchar(100) y ExternalAssetModel varchar(150).
// Sin esto, un valor más largo llega a Postgres (22001) y sale como 500 en vez de 400 (AUDIT.md H-10).
public class CheckOutRequestValidator : AbstractValidator<CheckOutRequest>
{
    // Mismo tope por pieza que aplica InventoryConsumptionService.
    private const int MaxQuantityPerPart = 100;
    private const int MaxParts = 100;

    public CheckOutRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Area).MaximumLength(150);

        // El servicio recorta (Trim) estos dos antes de guardar: se mide el valor ya recortado.
        RuleFor(x => x.ExternalAssetBrand)
            .Must(v => v!.Trim().Length <= 100)
            .WithMessage("'External Asset Brand' no debe superar los 100 caracteres.")
            .When(x => x.ExternalAssetBrand is not null);
        RuleFor(x => x.ExternalAssetModel)
            .Must(v => v!.Trim().Length <= 150)
            .WithMessage("'External Asset Model' no debe superar los 150 caracteres.")
            .When(x => x.ExternalAssetModel is not null);

        RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d).When(x => x.Longitude.HasValue);
        RuleFor(x => x.AccuracyMeters).GreaterThanOrEqualTo(0d).When(x => x.AccuracyMeters.HasValue);

        RuleFor(x => x.InitialCounterValue).GreaterThanOrEqualTo(0).When(x => x.InitialCounterValue.HasValue);
        RuleFor(x => x.ExistingConsumablesPrints).GreaterThanOrEqualTo(0).When(x => x.ExistingConsumablesPrints.HasValue);
        RuleFor(x => x.ExternalAssetCounter).GreaterThanOrEqualTo(0).When(x => x.ExternalAssetCounter.HasValue);

        RuleFor(x => x.Parts)
            .NotNull()
            .Must(p => p.Count <= MaxParts)
            .WithMessage($"No se pueden registrar más de {MaxParts} piezas por visita.");
        RuleForEach(x => x.Parts).ChildRules(part =>
        {
            part.RuleFor(p => p.ItemId).NotEmpty();
            part.RuleFor(p => p.Quantity).InclusiveBetween(1, MaxQuantityPerPart);
        });
    }
}
