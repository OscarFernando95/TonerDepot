using Toner.Application.Common.Interfaces;

namespace Toner.Application.Common;

// AsyncLocal (no un servicio scoped) porque el contexto tiene que seguir al flujo asíncrono hasta
// dentro del interceptor de conexión de EF Core, que se resuelve como singleton y no tiene acceso al
// scope de la request.
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<Func<TenantContext>?> CurrentFactory = new();

    public TenantContext? Current => CurrentFactory.Value?.Invoke();

    public IDisposable Push(Func<TenantContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        var previous = CurrentFactory.Value;
        CurrentFactory.Value = contextFactory;
        return new PopOnDispose(previous);
    }

    private sealed class PopOnDispose : IDisposable
    {
        private readonly Func<TenantContext>? _previous;
        private bool _disposed;

        public PopOnDispose(Func<TenantContext>? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentFactory.Value = _previous;
            _disposed = true;
        }
    }
}
