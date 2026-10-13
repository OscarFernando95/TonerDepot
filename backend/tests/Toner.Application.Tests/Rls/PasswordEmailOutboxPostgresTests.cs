using System.Security.Cryptography;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Common;
using Toner.Application.Common.Interfaces;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Users;
using Toner.Application.Users.Dtos;
using Toner.Domain.Common;
using Toner.Infrastructure.Auth;
using Toner.Infrastructure.Jobs;
using Toner.Infrastructure.Persistence;
using Toner.Infrastructure.Security;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-19: el usuario y el correo con su contraseña temporal se escribían en dos almacenes distintos (la
// base y las tablas de Hangfire) sin atomicidad. Contra el Postgres real y con el rol de la aplicación.
[Collection(nameof(RlsFixtureCollection))]
public class PasswordEmailOutboxPostgresTests
{
    private static readonly Guid CityId = RlsFixture.CityId;

    private readonly RlsFixture _fixture;

    public PasswordEmailOutboxPostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static readonly IJobPayloadEncryptor Encryptor = new AesGcmJobPayloadEncryptor(
        Options.Create(new JobPayloadEncryptionSettings { Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }));

    // Hangfire (o su almacenamiento) caído.
    private sealed class ThrowingJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => throw new InvalidOperationException("Hangfire caído (simulado).");
        public bool ChangeState(string jobId, IState state, string expectedState) => throw new InvalidOperationException("Hangfire caído (simulado).");
    }

    private sealed class RecordingJobClient : IBackgroundJobClient
    {
        public List<Job> Jobs { get; } = new();
        public string Create(Job job, IState state) { Jobs.Add(job); return Guid.NewGuid().ToString(); }
        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    // El scheduler que usa la aplicación en producción.
    private static IBackgroundJobScheduler ProductionScheduler(TonerDbContext db) => new OutboxPasswordEmailStager(db, Encryptor);

    private static TonerDbContext AppContext(TenantContextAccessor tenant, IBackgroundJobClient kickClient) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString)
            .AddInterceptors(
                new TenantContextInterceptor(tenant),
                new PasswordEmailOutboxKickInterceptor(kickClient, NullLogger<PasswordEmailOutboxKickInterceptor>.Instance))
            .Options);

    private static UserService Users(TonerDbContext db) =>
        new(db, new BCryptPasswordHasher(), ProductionScheduler(db), NullLogger<UserService>.Instance, TestSessions.Create(db));

    private static CreateUserRequest NewUser(string cedula) => new()
    {
        Cedula = cedula, FullName = "Usuario H19", Phone = "3000000000", Address = "Calle 1", CityId = CityId, RoleName = RoleNames.Coordinador
    };

    private static async Task CleanupAsync(string cedula)
    {
        await using var owner = OwnerContext();
        await owner.Database.ExecuteSqlRawAsync("DELETE FROM \"PasswordEmailOutbox\" WHERE \"Cedula\" = {0}", cedula);
        await owner.Database.ExecuteSqlRawAsync("DELETE FROM \"Users\" WHERE \"Cedula\" = {0}", cedula);
    }

    private static string NewCedula() => "9" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);

    private static async Task<List<Toner.Domain.Entities.PasswordEmailOutbox>> OutboxAsync(string cedula)
    {
        await using var owner = OwnerContext();
        return await owner.PasswordEmailOutbox.Where(o => o.Cedula == cedula).ToListAsync();
    }

    [PostgresFact]
    public async Task CrearUsuario_ConHangfireCaido_ElUsuarioSeCreaYElCorreoQuedaPendiente()
    {
        await _fixture.EnsureSeededAsync();
        var cedula = NewCedula();
        try
        {
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            // Hangfire caído también para el aviso inmediato: es best-effort y no debe tumbar la request.
            await using var db = AppContext(tenant, new ThrowingJobClient());

            var created = await Users(db).CreateAsync(NewUser(cedula));

            Assert.Equal(cedula, created.Cedula);
            await using var check = OwnerContext();
            Assert.True(await check.Users.AnyAsync(u => u.Cedula == cedula));

            // El correo quedó pendiente, cifrado, y se descifra a la contraseña que recibió el admin.
            var pending = Assert.Single(await OutboxAsync(cedula));
            Assert.NotEqual(created.GeneratedPassword, pending.EncryptedPassword);
            Assert.Equal(created.GeneratedPassword, Encryptor.Decrypt(pending.EncryptedPassword));
        }
        finally
        {
            await CleanupAsync(cedula);
        }
    }

    [PostgresFact]
    public async Task SiElUsuarioNoSePuedeGuardar_TampocoQuedaElCorreo()
    {
        // FK inválida en Users -> falla el INSERT en la base, en la MISMA transacción que la fila del outbox.
        await _fixture.EnsureSeededAsync();
        var cedula = NewCedula();
        try
        {
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            await using var db = AppContext(tenant, new RecordingJobClient());
            var request = NewUser(cedula);
            request.CityId = Guid.NewGuid();

            await Assert.ThrowsAsync<DbUpdateException>(() => Users(db).CreateAsync(request));

            await using var check = OwnerContext();
            Assert.False(await check.Users.AnyAsync(u => u.Cedula == cedula));
            Assert.Empty(await OutboxAsync(cedula));
        }
        finally
        {
            await CleanupAsync(cedula);
        }
    }

    [PostgresFact]
    public async Task ElGuardadoConCorreoPendiente_AvisaAlDespachador_YUnoSinCorreoNo()
    {
        await _fixture.EnsureSeededAsync();
        var cedula = NewCedula();
        try
        {
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            var kicks = new RecordingJobClient();
            await using var db = AppContext(tenant, kicks);

            var created = await Users(db).CreateAsync(NewUser(cedula));
            var kick = Assert.Single(kicks.Jobs);
            Assert.Equal(typeof(PasswordEmailOutboxDispatcher), kick.Type);

            await Users(db).UpdateAsync(created.Id, new UpdateUserRequest
            {
                Cedula = cedula, FullName = "Otro nombre", Phone = "3000000000", Address = "Calle 2", CityId = CityId
            });
            Assert.Single(kicks.Jobs); // sin fila de outbox, no hay aviso
        }
        finally
        {
            await CleanupAsync(cedula);
        }
    }

    [PostgresFact]
    public async Task Despachador_EncolaElJobExistente_ConLaMismaCedulaYCiphertext_YBorraLaFila()
    {
        var cedula = NewCedula();
        try
        {
            var ciphertext = Encryptor.Encrypt("Temporal#123");
            await using (var owner = OwnerContext())
            {
                owner.PasswordEmailOutbox.Add(new Toner.Domain.Entities.PasswordEmailOutbox { Cedula = cedula, EncryptedPassword = ciphertext });
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            var jobs = new RecordingJobClient();
            await using var db = AppContext(tenant, new RecordingJobClient());
            await new PasswordEmailOutboxDispatcher(db, jobs, tenant, NullLogger<PasswordEmailOutboxDispatcher>.Instance).RunAsync();

            var job = Assert.Single(jobs.Jobs, j => (string)j.Args[0] == cedula);
            Assert.Equal(typeof(SendGeneratedPasswordEmailJob), job.Type);
            Assert.Equal(nameof(SendGeneratedPasswordEmailJob.RunAsync), job.Method.Name);
            Assert.Equal(ciphertext, job.Args[1]);
            Assert.Empty(await OutboxAsync(cedula));
        }
        finally
        {
            await CleanupAsync(cedula);
        }
    }

    [PostgresFact]
    public async Task Despachador_SiHangfireFalla_LaFilaSeQuedaParaElSiguienteIntento()
    {
        var cedula = NewCedula();
        try
        {
            await using (var owner = OwnerContext())
            {
                owner.PasswordEmailOutbox.Add(new Toner.Domain.Entities.PasswordEmailOutbox { Cedula = cedula, EncryptedPassword = Encryptor.Encrypt("x") });
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            await using var db = AppContext(tenant, new RecordingJobClient());
            await new PasswordEmailOutboxDispatcher(db, new ThrowingJobClient(), tenant, NullLogger<PasswordEmailOutboxDispatcher>.Instance).RunAsync();

            Assert.Single(await OutboxAsync(cedula));
        }
        finally
        {
            await CleanupAsync(cedula);
        }
    }
}
