namespace Toner.Application.Common.Interfaces;

// Application define QUÉ hay que enviar; Infrastructure decide CÓMO (outbox + Hangfire, y cómo protege la
// contraseña antes de persistirla) — mismo principio que IExceptionLogger. UserService nunca ve Hangfire ni
// cómo se cifra la contraseña.
public interface IBackgroundJobScheduler
{
    // DEJA PREPARADO el correo con la contraseña temporal en el contexto compartido; NO guarda nada por sí
    // mismo. Debe llamarse ANTES del SaveChanges del caller: así el correo pendiente se persiste en la misma
    // transacción que el usuario (o no se persiste ninguno). Antes se encolaba en Hangfire DESPUÉS del
    // SaveChanges, y si eso fallaba el usuario quedaba creado sin que la contraseña llegara por ninguna vía
    // (AUDIT.md H-19).
    //
    // La contraseña llega en claro porque este es el límite entre Application e Infrastructure: la
    // implementación es responsable de que NUNCA llegue a un almacenamiento durable sin cifrar (hallazgo #3 de
    // SECURITY_AUDIT.md).
    void StageGeneratedPasswordEmail(string cedula, string generatedPassword);
}
