using FluentValidation;
using Toner.Application.Assets.Dtos;

namespace Toner.Application.Assets.Validators;

public class CreateAssetBrandRequestValidator : AbstractValidator<CreateAssetBrandRequest>
{
    public CreateAssetBrandRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
