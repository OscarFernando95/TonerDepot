namespace Toner.Application.Auth.Dtos;

public class LoginResult
{
    public bool Succeeded { get; private init; }
    public string? Token { get; private init; }
    public DateTime? ExpiresAtUtc { get; private init; }
    public CurrentUserDto? User { get; private init; }

    public static LoginResult Success(string token, DateTime expiresAtUtc, CurrentUserDto user) => new()
    {
        Succeeded = true,
        Token = token,
        ExpiresAtUtc = expiresAtUtc,
        User = user
    };

    public static LoginResult Failure() => new() { Succeeded = false };
}
