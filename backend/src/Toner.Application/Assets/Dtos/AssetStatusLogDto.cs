namespace Toner.Application.Assets.Dtos;

public class AssetStatusLogDto
{
    public Guid Id { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? ChangedByUserName { get; set; }
    public string? Notes { get; set; }
}
