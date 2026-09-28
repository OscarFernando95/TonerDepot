using Toner.Application.Auth.Dtos;

namespace Toner.Application.Auth;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, string? ipAddress = null, CancellationToken cancellationToken = default, string? clientType = null, string? userAgent = null);
    Task LogoutAsync(Guid userId, Guid sessionId, string? ipAddress = null, CancellationToken cancellationToken = default);
    Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string? ipAddress = null, CancellationToken cancellationToken = default);
}
