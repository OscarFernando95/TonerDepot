namespace Toner.Application.Contracts.Dtos;

public class SetContractStatusRequest
{
    // Nombre del enum Toner.Domain.Enums.ContractStatus (Activo, Vencido, Cancelado).
    public string Status { get; set; } = string.Empty;
}
