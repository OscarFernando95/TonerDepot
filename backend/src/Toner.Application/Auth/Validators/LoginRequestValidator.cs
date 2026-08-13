using FluentValidation;
using Toner.Application.Auth.Dtos;

namespace Toner.Application.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Cedula).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
