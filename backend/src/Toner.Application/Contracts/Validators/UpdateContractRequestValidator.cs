using FluentValidation;
using Toner.Application.Contracts.Dtos;

namespace Toner.Application.Contracts.Validators;

public class UpdateContractRequestValidator : AbstractValidator<UpdateContractRequest>
{
    public UpdateContractRequestValidator()
    {
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("EndDate no puede ser anterior a StartDate.")
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.IncludedPrintsPerMonth).GreaterThanOrEqualTo(0).When(x => x.IncludedPrintsPerMonth.HasValue);
        RuleFor(x => x.PricePerExtraPage).GreaterThanOrEqualTo(0).When(x => x.PricePerExtraPage.HasValue);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
