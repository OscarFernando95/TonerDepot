using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Correo de contraseña temporal pendiente de encolar (AUDIT.md H-19). Se escribe en el MISMO SaveChanges que
// crea o resetea el usuario, así que o quedan ambos o ninguno; PasswordEmailOutboxDispatcher lo pasa luego a
// Hangfire y borra la fila. La contraseña viaja SIEMPRE cifrada (AES-GCM, la misma clave que los jobs).
public class PasswordEmailOutbox : BaseEntity
{
    public string Cedula { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
}
