using FluentValidation;
using Toner.Application.Assets.Dtos;

namespace Toner.Application.Assets.Validators;

public class UpdateAssetModelRequestValidator : AbstractValidator<UpdateAssetModelRequest>
{
    public UpdateAssetModelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.GeneralPrintThreshold).GreaterThan(0);
        RuleFor(x => x.GeneralMonthsInterval).GreaterThan(0);
        RuleFor(x => x.UnitsPrintThreshold).GreaterThan(0);
        RuleFor(x => x.UnitsMonthsInterval).GreaterThan(0);
        RuleFor(x => x.ConsumablesPrintThreshold).GreaterThan(0);
    }
}
