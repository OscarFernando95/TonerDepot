using Toner.Application.Assets.Dtos;
using Toner.Application.Common;
using Toner.Domain.Entities;

namespace Toner.Application.Assets;

public interface IAssetService
{
    Task<AssetDto> CreateAsync(CreateAssetRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetDto>> ListAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
    Task<AssetDto> GetByIdAsync(RequestingUser requestingUser, Guid id, CancellationToken cancellationToken = default);
    Task<AssetDto> UpdateAsync(Guid id, UpdateAssetRequest request, CancellationToken cancellationToken = default);
    Task<AssetDto> ChangeStatusAsync(Guid id, ChangeAssetStatusRequest request, Guid changedByUserId, CancellationToken cancellationToken = default);

    // Igual que ChangeStatusAsync pero no guarda — para que un caller (ContractAssetService) pueda
    // combinar esta mutación con la suya propia en un solo SaveChangesAsync atómico.
    Task<Asset> PrepareStatusChangeAsync(Guid id, ChangeAssetStatusRequest request, Guid changedByUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AssetStatusLogDto>> GetStatusHistoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MeterReadingDto> AddMeterReadingAsync(
        Guid id,
        CreateMeterReadingRequest request,
        RequestingUser requestingUser,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MeterReadingDto>> GetMeterReadingsAsync(Guid id, CancellationToken cancellationToken = default);

    // Lista abierta entre los técnicos que cubren la ciudad del activo (TechnicianCoverage) — no hay
    // asignación previa, cualquiera de ellos puede tomarla.
    Task<IReadOnlyList<PendingInstallationDto>> ListPendingInstallationsAsync(Guid technicianId, CancellationToken cancellationToken = default);

    // Para el módulo "Lectura de contadores" (abierto a los 5 roles) — separado de ListAsync a propósito,
    // para no tocar el alcance por rol que ya usan los consumidores existentes de ListAsync.
    Task<IReadOnlyList<MeterReadingAssetDto>> ListForMeterReadingAsync(RequestingUser requestingUser, CancellationToken cancellationToken = default);
}
