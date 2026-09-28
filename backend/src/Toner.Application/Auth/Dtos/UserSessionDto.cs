namespace Toner.Application.Auth.Dtos;

public class UserSessionDto
{
    public Guid Id { get; set; }
    public string ClientType { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public bool IsActive { get; set; }
}
