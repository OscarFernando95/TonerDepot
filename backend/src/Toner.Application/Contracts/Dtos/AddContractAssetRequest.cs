namespace Toner.Application.Contracts.Dtos;

public class AddContractAssetRequest
{
    public Guid AssetId { get; set; }
    public DateTime? StartDate { get; set; }
}
