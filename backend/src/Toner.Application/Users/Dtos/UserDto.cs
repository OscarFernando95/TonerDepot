namespace Toner.Application.Users.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public string Cedula { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public Guid? CityId { get; set; }
    public string? CityName { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? TechnicianId { get; set; }
    public DateTime CreatedAt { get; set; }
}
