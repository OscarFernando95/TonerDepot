namespace Toner.Infrastructure.Security;

public class JobPayloadEncryptionSettings
{
    // Base64 de 32 bytes (AES-256). Propia, en IConfiguration — no ASP.NET Data Protection: su key
    // ring puede no persistir entre reinicios del contenedor y dejaría sin poder descifrarse un job
    // pendiente tras un redeploy (decisión explícita, SECURITY_AUDIT.md hallazgo #3).
    public string Key { get; set; } = string.Empty;
}
