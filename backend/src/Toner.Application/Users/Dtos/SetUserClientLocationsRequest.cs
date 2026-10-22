namespace Toner.Application.Users.Dtos;

// Sedes de su empresa que puede ver y en las que puede reportar un usuario Cliente.
public class SetUserClientLocationsRequest
{
    // null: sin restricción, ve todas las sedes de su empresa. Con valores: solo esas (no vacía).
    public IReadOnlyList<Guid>? AllowedLocationIds { get; set; }
}
