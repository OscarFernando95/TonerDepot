namespace Toner.Application.Contracts.Dtos;

public class AddContractAssetRequest
{
    public Guid AssetId { get; set; }

    // Sede de destino: el activo pasa a PendienteInstalacion ya asociado a esta sede (ver
    // ContractAssetService.AddAsync). Debe pertenecer al mismo cliente del contrato.
    public Guid ClientLocationId { get; set; }

    public DateTime? StartDate { get; set; }
}
