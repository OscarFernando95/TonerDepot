namespace Toner.Application.Contracts.Dtos;

// Al finalizar el vínculo de un activo con un contrato, el admin decide ahí mismo qué pasa con el
// activo: el motivo queda en su historial de estados (AssetStatusLog.Notes), y el destino es
// obligatoriamente Bodega o Dado de baja — nunca queda "flotando" instalado sin contrato.
public class EndContractAssetRequest
{
    public string Reason { get; set; } = string.Empty;

    // "EnBodega" | "DadoDeBaja" — ver EndContractAssetRequestValidator.
    public string NewAssetStatus { get; set; } = string.Empty;
}
