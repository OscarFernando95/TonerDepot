using Toner.Application.Dashboard.Dtos;

namespace Toner.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(int periodDays, CancellationToken cancellationToken = default);
}
