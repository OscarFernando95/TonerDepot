namespace Toner.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} con clave '{key}' no fue encontrado.")
    {
    }

    // Para cuando el recurso existió pero ya no está disponible por una razón que el usuario debe conocer
    // (p. ej. una foto purgada por la política de retención, AUDIT.md H-44).
    public NotFoundException(string message)
        : base(message)
    {
    }
}
