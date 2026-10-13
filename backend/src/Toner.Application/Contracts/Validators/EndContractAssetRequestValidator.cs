using FluentValidation;
using Toner.Application.Contracts.Dtos;
using Toner.Domain.Enums;

namespace Toner.Application.Contracts.Validators;

public class EndContractAssetRequestValidator : AbstractValidator<EndContractAssetRequest>
{
    public EndContractAssetRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("El motivo de la finalización es obligatorio.")
            .MaximumLength(500);

        // Solo estos dos: al finalizar el vínculo el activo nunca se queda "Instalado" sin contrato
        // ni pasa directo a otro estado que no sea uno de estos dos.
        RuleFor(x => x.NewAssetStatus)
            .Must(s => s is nameof(AssetLifecycleStatus.EnBodega) or nameof(AssetLifecycleStatus.DadoDeBaja))
            .WithMessage("NewAssetStatus debe ser 'EnBodega' o 'DadoDeBaja'.");
    }
}
