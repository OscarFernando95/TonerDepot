namespace Toner.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} con clave '{key}' no fue encontrado.")
    {
    }
}
