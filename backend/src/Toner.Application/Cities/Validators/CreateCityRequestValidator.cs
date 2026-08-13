using FluentValidation;
using Toner.Application.Cities.Dtos;

namespace Toner.Application.Cities.Validators;

public class CreateCityRequestValidator : AbstractValidator<CreateCityRequest>
{
    public CreateCityRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.StateOrProvince).MaximumLength(150);
    }
}
