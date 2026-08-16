using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Toner.Api.Middleware;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;

namespace Toner.Api.Tests;

// Verifica el hallazgo #8 de SECURITY_AUDIT.md: antes de este fix, una ForbiddenException (ej. un
// Cliente intentando ver datos de otro cliente) llegaba al ExceptionHandlingMiddleware, se
// traducía a un 403, y no dejaba ningún rastro — ni en consola, ni en ExceptionLogs (que solo
// persiste los 500). Ahora debe quedar una entrada Warning con el usuario y el endpoint.
public class ExceptionHandlingMiddlewareTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static async Task<(TestServer Server, CapturingLogger<ExceptionHandlingMiddleware> Logger)> CreateTestServerAsync()
    {
        var capturingLogger = new CapturingLogger<ExceptionHandlingMiddleware>();

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ILogger<ExceptionHandlingMiddleware>>(capturingLogger);
                    services.AddScoped<IExceptionLogger, NoOpExceptionLogger>();
                });
                webBuilder.Configure(app =>
                {
                    // Simula un usuario ya autenticado (equivalente a lo que dejaría UseAuthentication
                    // con un JWT válido) para poder verificar que el log captura UserId y rol.
                    app.Use(async (context, next) =>
                    {
                        var identity = new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, TestUserId.ToString()),
                            new Claim(ClaimTypes.Role, "Cliente")
                        }, authenticationType: "TestAuth");
                        context.User = new ClaimsPrincipal(identity);
                        await next();
                    });

                    app.UseMiddleware<ExceptionHandlingMiddleware>();

                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/contracts/{id}", (HttpContext _) =>
                            throw new ForbiddenException("Este contrato no pertenece a tu cliente."));
                    });
                });
            })
            .StartAsync();

        return (host.GetTestServer(), capturingLogger);
    }

    [Fact]
    public async Task ForbiddenException_QuedaRegistradaComoWarningConUsuarioYEndpoint()
    {
        var (server, logger) = await CreateTestServerAsync();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/api/contracts/abc123");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(TestUserId, entry.Properties["UserId"]);
        Assert.Equal("Cliente", entry.Properties["UserRole"]);
        Assert.Equal("/api/contracts/abc123", entry.Properties["Path"]?.ToString());
        Assert.Equal("GET", entry.Properties["Method"]);
    }

    private sealed class NoOpExceptionLogger : IExceptionLogger
    {
        public Task LogAsync(
            string source,
            Exception exception,
            string? requestMethod = null,
            string? requestPath = null,
            int? statusCode = null,
            Guid? userId = null,
            string? userEmail = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
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
}
