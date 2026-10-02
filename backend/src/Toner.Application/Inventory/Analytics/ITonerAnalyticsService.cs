using Toner.Application.Common.Paging;

namespace Toner.Application.Inventory.Analytics;

public interface ITonerAnalyticsService
{
    Task<TonerSummaryDto> GetSummaryAsync(TonerFilter filter, CancellationToken cancellationToken = default);
    Task<PagedResult<TonerMachineRowDto>> ListMachinesAsync(TonerFilter filter, int? page, int? pageSize, CancellationToken cancellationToken = default);

    // Todas las máquinas del filtro en CSV (separador ";" y BOM UTF-8, para que Excel en español lo abra bien).
    Task<string> ExportCsvAsync(TonerFilter filter, CancellationToken cancellationToken = default);
}
