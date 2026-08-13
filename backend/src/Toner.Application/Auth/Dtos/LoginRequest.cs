namespace Toner.Application.Auth.Dtos;

public class LoginRequest
{
    public string Cedula { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
