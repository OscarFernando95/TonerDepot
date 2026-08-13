using FluentValidation;
using Toner.Application.Assets.Dtos;

namespace Toner.Application.Assets.Validators;

public class CreateMeterReadingRequestValidator : AbstractValidator<CreateMeterReadingRequest>
{
    public CreateMeterReadingRequestValidator()
    {
        RuleFor(x => x.CounterValue).GreaterThanOrEqualTo(0);
    }
}
