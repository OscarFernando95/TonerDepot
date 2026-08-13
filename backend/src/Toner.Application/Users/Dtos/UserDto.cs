namespace Toner.Application.Users.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? TechnicianId { get; set; }
    public DateTime CreatedAt { get; set; }
}
