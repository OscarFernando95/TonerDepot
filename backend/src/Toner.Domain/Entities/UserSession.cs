using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Una fila por JWT emitido (Id == claim "jti"). Da tres cosas que un JWT sin estado no tiene:
// revocación real (logout, cierre por admin), sesión única por técnico, y auditoría de quién estuvo
// conectado, desde dónde y cuándo. Fuera de RLS igual que Users: se lee en el login y en el
// validador del token, ambos con contexto Anonymous, y solo guarda metadatos de sesión.
public class UserSession : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // "web" | "mobile" | "unknown" — lo declara el cliente con X-Client-Type; es informativo
    // (mensaje al usuario y auditoría), nunca una decisión de seguridad.
    public string ClientType { get; set; } = "unknown";
    // SHA-256 (hex) del identificador de instalación del teléfono que declara la app (X-Device-Id). Existe para una sola
    // cosa: que reinstalar la app en el MISMO teléfono reemplace su propia sesión en vez de quedar bloqueado por ella.
    // Se guarda hasheado para que quien lea la tabla no pueda reutilizarlo. Es declarado por el cliente, así que nunca
    // concede acceso por sí solo: solo se mira tras validar cédula y contraseña, y solo para cerrar una sesión del mismo
    // técnico. Null en la web y en sesiones anteriores a esta columna.
    public string? DeviceIdHash { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    // Null mientras la sesión sigue viva. Nunca se borra la fila: es el registro de auditoría.
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }

    // Snapshot de la política al emitir la sesión (así cambiar la configuración no reinterpreta
    // sesiones ya emitidas). true → a lo sumo una sesión viva por usuario (índice único parcial en
    // BD, que es lo que cierra la carrera entre dos logins simultáneos).
    public bool EnforcesSingleSession { get; set; }

    // Minutos de inactividad tras los cuales la sesión deja de ser válida; null = sin límite.
    public int? IdleTimeoutMinutes { get; set; }
}
