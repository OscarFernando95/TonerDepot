using FluentValidation;
using Toner.Application.Push.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Push.Validators;

public class RegisterDeviceTokenRequestValidator : AbstractValidator<RegisterDeviceTokenRequest>
{
    public RegisterDeviceTokenRequestValidator()
    {
        RuleFor(x => x.Platform)
            .NotEmpty()
            .Must(p => Enum.TryParse<PushPlatform>(p, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("La plataforma debe ser Android o iOS.");

        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
    }
}
