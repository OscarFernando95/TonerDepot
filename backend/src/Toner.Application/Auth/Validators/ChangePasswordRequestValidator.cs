using FluentValidation;
using Toner.Application.Auth.Dtos;

namespace Toner.Application.Auth.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();

        // NIST 800-63B prioriza longitud sobre complejidad forzada — sin símbolo obligatorio, pero
        // con un mínimo más alto que el anterior (8) y una mezcla mínima de tipos de carácter
        // (SECURITY_AUDIT.md hallazgo #15). No aplica a PasswordDefaults.DefaultPassword (la
        // contraseña genérica de reset/creación): ese es el hallazgo #3, diferido a propósito.
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(10)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir al menos una letra mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir al menos una letra minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir al menos un número.");
    }
}
