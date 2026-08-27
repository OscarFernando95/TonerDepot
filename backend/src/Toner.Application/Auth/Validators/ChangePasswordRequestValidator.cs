using FluentValidation;
using Toner.Application.Auth.Dtos;
using Toner.Domain.Common;

namespace Toner.Application.Auth.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();

        // NIST 800-63B prioriza longitud sobre complejidad forzada — sin símbolo obligatorio, pero
        // con un mínimo más alto que el anterior (8) y una mezcla mínima de tipos de carácter
        // (SECURITY_AUDIT.md hallazgo #15). PasswordDefaults.MinimumLength es también lo que
        // SecurePasswordGenerator usa para la contraseña de creación/reset (hallazgo #3): un solo
        // número, no dos que puedan desincronizarse.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(PasswordDefaults.MinimumLength)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
