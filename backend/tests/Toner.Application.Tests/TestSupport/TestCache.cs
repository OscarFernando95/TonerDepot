using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;

namespace Toner.Application.Tests.TestSupport;

public static class TestCache
{
    // Caché real (no un doble): lo que se prueba es el comportamiento de caché de los servicios, así
    // que sustituir IMemoryCache por un mock probaría el mock. Cada llamada devuelve una instancia
    // NUEVA para que un test no herede lo que dejó otro.
    public static IMemoryCache New() => new MemoryCache(new MemoryCacheOptions());

    public static ITenantContextAccessor StaffTenant() => new FixedTenantContextAccessor(TenantContext.Staff);

    public static ITenantContextAccessor ClientTenant(Guid clientId) =>
        new FixedTenantContextAccessor(TenantContext.ForClient(clientId));

    // El TenantContextAccessor real usa AsyncLocal y un scope con Push/Dispose; para un test unitario
    // basta con un contexto fijo.
    private sealed class FixedTenantContextAccessor : ITenantContextAccessor
    {
        private readonly TenantContext _context;

        public FixedTenantContextAccessor(TenantContext context) => _context = context;

        public TenantContext? Current => _context;

        public IDisposable Push(Func<TenantContext> contextFactory) => new NoOpScope();

        private sealed class NoOpScope : IDisposable
        {
            public void Dispose() { }
        }
    }
}
