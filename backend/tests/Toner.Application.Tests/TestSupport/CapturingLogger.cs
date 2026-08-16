using Microsoft.Extensions.Logging;

namespace Toner.Application.Tests.TestSupport;

// Fake de ILogger<T> que captura nivel + propiedades estructuradas de cada log, en vez de solo el
// string final formateado — así los tests pueden verificar campos concretos (Cedula, IpAddress,
// Reason, UserId, ...) sin depender de la redacción exacta del mensaje.
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = (state as IEnumerable<KeyValuePair<string, object?>>)?
            .ToDictionary(kv => kv.Key, kv => kv.Value)
            ?? new Dictionary<string, object?>();

        Entries.Add(new LogEntry(logLevel, formatter(state, exception), properties));
    }

    public sealed record LogEntry(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties);
}
