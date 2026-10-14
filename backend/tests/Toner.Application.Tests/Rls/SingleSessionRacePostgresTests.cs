using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Auth;
using Toner.Application.Auth.Dtos;
using Toner.Application.Common.Exceptions;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-40: dos logins simultáneos del mismo técnico pasan ambos el chequeo de "sesión activa" y el índice
// único parcial deja entrar solo a uno. El mensaje para el segundo debía ser el legible, pero el catch de
// AuthService nunca se alcanzaba (TonerDbContext ya convertía la violación en un ConflictException genérico).
[Collection(nameof(RlsFixtureCollection))]
public class SingleSessionRacePostgresTests
{
    private static readonly BCryptPasswordHasher Hasher = new();

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    // Users y UserSessions no tienen RLS (excepción registrada): el login corre con contexto Anonymous, así que
    // aquí basta el rol de la aplicación sin interceptor de tenencia.
    private static TonerDbContext AppContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(interceptors)
            .Options);

    private static AuthService Auth(TonerDbContext db) =>
        new(db, Hasher, new JwtTokenGenerator(Options.Create(new JwtSettings
        {
            Issuer = "test-issuer", Audience = "test-audience",
            SigningKey = "test-signing-key-at-least-32-characters-long", ExpiryMinutes = 60
        })), NullLogger<AuthService>.Instance, TestSessions.Create(db));

    // Pausa el login en el que está instalado justo DESPUÉS de consultar las sesiones abiertas (no vio ninguna)
    // y deja que otro login termine entero antes de seguir: la intercalación exacta de la carrera.
    private sealed class PauseAfterSessionsRead : DbCommandInterceptor
    {
        private readonly Func<Task> _runOther;
        private bool _fired;

        public PauseAfterSessionsRead(Func<Task> runOther) => _runOther = runOther;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!_fired && command.CommandText.Contains("FROM \"UserSessions\""))
            {
                _fired = true;
                await _runOther();
            }

            return result;
        }
    }

    [PostgresFact]
    public async Task DosLoginsSimultaneosDelMismoTecnico_ElSegundoRecibeElMensajeLegible()
    {
        // No usa RlsFixture: asegura por su cuenta el rol Tecnico, que una base recién migrada no tiene.
        await RlsFixture.EnsureRolesAsync();
        Guid userId = Guid.Empty;
        var cedula = "7" + Random.Shared.Next(100_000_000, 999_999_999);
        try
        {
            await using (var owner = OwnerContext())
            {
                var role = await owner.Roles.FirstAsync(r => r.Name == RoleNames.Tecnico);
                var user = TestEntities.User(role, cedula: cedula);
                user.PasswordHash = Hasher.Hash("Password123!");
                owner.Add(user);
                await owner.SaveChangesAsync();
                userId = user.Id;
            }

            var login = new LoginRequest { Cedula = cedula, Password = "Password123!" };

            async Task RunA()
            {
                await using var dbA = AppContext();
                Assert.True((await Auth(dbA).LoginAsync(login, clientType: "mobile")).Succeeded);
            }

            await using var dbB = AppContext(new PauseAfterSessionsRead(RunA));
            var ex = await Assert.ThrowsAsync<ConflictException>(() => Auth(dbB).LoginAsync(login, clientType: "web"));

            Assert.Contains("Ya tienes una sesión activa", ex.Message);
            Assert.DoesNotContain("IX_UserSessions", ex.Message);
        }
        finally
        {
            if (userId != Guid.Empty)
            {
                await using var cleanup = OwnerContext();
                await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"UserSessions\" WHERE \"UserId\" = {0}", userId);
                await cleanup.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Id\" = {0}", userId);
            }
        }
    }
}
