namespace Toner.Application.Common.Exceptions;

// Para autorización de grano fino (ej. un Cliente accediendo a datos de otro cliente) que
// [Authorize(Roles = ...)] no puede expresar por sí solo.
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
