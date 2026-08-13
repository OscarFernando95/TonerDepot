using Microsoft.EntityFrameworkCore;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.TestSupport;

// Cada test usa un nombre de base de datos único (Guid) para no compartir estado entre tests,
// y crea contextos nuevos por fase (arrange/act) para simular el ciclo de vida real de un
// DbContext por request en ASP.NET Core, en vez de reutilizar el mismo tracker en todo el test.
public static class TonerTestDb
{
    public static TonerDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<TonerDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new TonerDbContext(options);
    }
}
