using FluentValidation;
using Toner.Application.Contracts.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Contracts.Validators;

public class SetContractStatusRequestValidator : AbstractValidator<SetContractStatusRequest>
{
    public SetContractStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty()
            .Must(s => Enum.TryParse<ContractStatus>(s, out _))
            .WithMessage($"Status debe ser uno de: {string.Join(", ", Enum.GetNames<ContractStatus>())}.");
    }
}
