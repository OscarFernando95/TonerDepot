namespace Toner.Application.Common.Interfaces;

// Application define QUÉ hay que encolar; Infrastructure decide CÓMO (Hangfire, y cómo protege el
// argumento antes de que Hangfire lo persista) — mismo principio que IExceptionLogger. UserService
// nunca ve Hangfire ni cómo se cifra la contraseña en tránsito hacia el almacenamiento del job.
public interface IBackgroundJobScheduler
{
    // La contraseña llega en texto plano acá porque este es el límite entre Application e
    // Infrastructure: la implementación es responsable de que NUNCA cruce a un almacenamiento durable
    // (tablas de Hangfire) sin cifrar (hallazgo #3 de SECURITY_AUDIT.md).
    void EnqueueGeneratedPasswordEmail(string cedula, string generatedPassword);
}
