using Toner.Application.Common.Interfaces;

namespace Toner.Application.Tests.TestSupport;

public sealed class FakeExceptionLogger : IExceptionLogger
{
    public List<(string Source, Exception Exception)> LoggedExceptions { get; } = new();

    public Task LogAsync(
        string source,
        Exception exception,
        string? requestMethod = null,
        string? requestPath = null,
        int? statusCode = null,
        Guid? userId = null,
        string? userEmail = null,
        CancellationToken cancellationToken = default)
    {
        LoggedExceptions.Add((source, exception));
        return Task.CompletedTask;
    }
}
