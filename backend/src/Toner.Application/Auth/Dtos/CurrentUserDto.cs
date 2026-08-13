namespace Toner.Application.Auth.Dtos;

public class CurrentUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public Guid? TechnicianId { get; set; }
}
