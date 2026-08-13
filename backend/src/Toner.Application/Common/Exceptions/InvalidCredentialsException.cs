namespace Toner.Application.Common.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Las credenciales no son válidas.")
    {
    }
}
