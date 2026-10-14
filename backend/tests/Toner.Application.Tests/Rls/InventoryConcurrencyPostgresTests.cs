using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-17: el traspaso lee el saldo del origen y luego inserta. Contra el Postgres real y con el rol de la
// aplicación se reproduce la carrera de forma determinista: sin bloqueo el origen queda en negativo; con el
// pg_advisory_xact_lock por (ítem, origen) el segundo traspaso ve el saldo real y recibe un 409.
[Collection(nameof(RlsFixtureCollection))]
public class InventoryConcurrencyPostgresTests
{
    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    // Mismo EnableRetryOnFailure que Program.cs: el lock tiene que funcionar bajo el execution strategy real.
    private static TonerDbContext AppContext(TenantContextAccessor tenant, params IInterceptor[] extra) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(50), errorCodesToAdd: null))
            .AddInterceptors(new IInterceptor[] { new TenantContextInterceptor(tenant) }.Concat(extra))
            .Options);

    private static InventoryService Service(TonerDbContext db) => new(db, new PostgresStockLock(db));

    // Pausa el traspaso en el que está instalado justo DESPUÉS de leer el saldo y lanza el otro traspaso,
    // esperándolo como máximo `wait`. Sin bloqueo, el otro termina dentro de la espera (carrera real). Con
    // bloqueo, el otro queda esperando el lock que este ya tiene: la espera acotada evita el deadlock del test.
    private sealed class PauseAfterBalanceRead : DbCommandInterceptor
    {
        private readonly Func<Task> _startOther;
        private readonly TimeSpan _wait;
        private bool _fired;
        public Task? Other { get; private set; }

        public PauseAfterBalanceRead(Func<Task> startOther, TimeSpan wait)
        {
            _startOther = startOther;
            _wait = wait;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!_fired && command.CommandText.Contains("sum(", StringComparison.OrdinalIgnoreCase)
                        && command.CommandText.Contains("\"InventoryMovements\""))
            {
                _fired = true;
                Other = _startOther();
                await Task.WhenAny(Other, Task.Delay(_wait, cancellationToken));
            }

            return result;
        }
    }

    private sealed record Seed(Guid ItemId, Guid MainId, Guid ZoneLocationId, Guid ZoneId, string ItemName);

    private static async Task<Seed> SeedAsync(int initialStock)
    {
        var name = "PG-H17-" + Guid.NewGuid().ToString("N")[..8];
        await using var owner = OwnerContext();
        var mainId = (await owner.InventoryLocations.FirstAsync(l => l.Kind == InventoryLocationKind.Principal)).Id;
        var zone = new Zone { Name = name };
        var zoneLocation = new InventoryLocation { Kind = InventoryLocationKind.Zona, Zone = zone };
        var item = new InventoryItem { Name = name, Category = InventoryCategory.Repuesto };
        owner.AddRange(zone, zoneLocation, item);
        owner.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItem = item, InventoryLocationId = mainId, Type = InventoryMovementType.Entrada, Delta = initialStock
        });
        await owner.SaveChangesAsync();
        return new Seed(item.Id, mainId, zoneLocation.Id, zone.Id, name);
    }

    private static async Task CleanupAsync(Seed? seed)
    {
        if (seed is null) return;
        await using var owner = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
        await owner.OpenAsync();
        foreach (var sql in new[]
        {
            @"DELETE FROM ""InventoryMovements"" WHERE ""InventoryItemId"" = @i",
            @"DELETE FROM ""InventoryItems"" WHERE ""Id"" = @i",
            @"DELETE FROM ""InventoryLocations"" WHERE ""Id"" = @l",
            @"DELETE FROM ""Zones"" WHERE ""Id"" = @z"
        })
        {
            await using var cmd = new NpgsqlCommand(sql, owner);
            cmd.Parameters.AddWithValue("i", seed.ItemId);
            cmd.Parameters.AddWithValue("l", seed.ZoneLocationId);
            cmd.Parameters.AddWithValue("z", seed.ZoneId);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> BalanceAsync(Guid itemId, Guid locationId)
    {
        await using var owner = OwnerContext();
        return await owner.InventoryMovements
            .Where(m => m.InventoryItemId == itemId && m.InventoryLocationId == locationId)
            .SumAsync(m => m.Delta);
    }

    private static TransferRequest Transfer(Seed seed, int quantity) => new()
    {
        ItemId = seed.ItemId, FromLocationId = seed.MainId, ToLocationId = seed.ZoneLocationId, Quantity = quantity
    };

    [PostgresFact]
    public async Task DosTraspasosSimultaneos_DelMismoOrigen_NuncaDejanSaldoNegativo()
    {
        Seed? seed = null;
        try
        {
            seed = await SeedAsync(initialStock: 10);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            // Traspaso A (8 unidades): se lanza mientras B está pausado después de leer el saldo.
            async Task RunA()
            {
                await using var dbA = AppContext(tenant);
                await Service(dbA).TransferAsync(Transfer(seed, 8), Guid.NewGuid());
            }

            var pause = new PauseAfterBalanceRead(RunA, TimeSpan.FromSeconds(2));
            await using var dbB = AppContext(tenant, pause);
            var b = await Record.ExceptionAsync(() => Service(dbB).TransferAsync(Transfer(seed, 8), Guid.NewGuid()));
            var a = await Record.ExceptionAsync(() => pause.Other!);

            // Exactamente uno pasa y el otro recibe "Saldo insuficiente".
            var outcomes = new[] { a, b };
            Assert.Single(outcomes, e => e is null);
            Assert.Single(outcomes, e => e is ConflictException);
            Assert.Equal(2, await BalanceAsync(seed.ItemId, seed.MainId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [PostgresFact]
    public async Task TraspasosDeItemsDistintos_NoSeEsperanEntreSi()
    {
        // El lock es por (ítem, origen): un traspaso de OTRO ítem no debe quedar bloqueado. Si lo estuviera,
        // A no terminaría dentro de la espera y este test lo detectaría por tiempo de espera.
        Seed? first = null, second = null;
        try
        {
            first = await SeedAsync(initialStock: 10);
            second = await SeedAsync(initialStock: 10);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            var aFinished = false;
            async Task RunA()
            {
                await using var dbA = AppContext(tenant);
                await Service(dbA).TransferAsync(Transfer(second, 8), Guid.NewGuid());
                aFinished = true;
            }

            var pause = new PauseAfterBalanceRead(RunA, TimeSpan.FromSeconds(10));
            await using var dbB = AppContext(tenant, pause);
            await Service(dbB).TransferAsync(Transfer(first, 8), Guid.NewGuid());

            Assert.True(aFinished, "El traspaso de otro ítem quedó bloqueado: el lock no es por (ítem, origen).");
            Assert.Equal(2, await BalanceAsync(first.ItemId, first.MainId));
            Assert.Equal(2, await BalanceAsync(second.ItemId, second.MainId));
        }
        finally
        {
            await CleanupAsync(first);
            await CleanupAsync(second);
        }
    }

    // Error de red simulado: NpgsqlException con una IOException dentro es "transitorio" para Npgsql, así que
    // el execution strategy reintenta igual que ante un corte real.
    private static NpgsqlException TransientError() => new("Corte de red simulado (test).", new IOException("simulado"));

    // Falla UNA vez el primer INSERT de movimientos: el intento se aborta antes del commit y se reintenta.
    private sealed class FailFirstMovementInsert : DbCommandInterceptor
    {
        private bool _failed;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!_failed && command.CommandText.Contains("INSERT INTO \"InventoryMovements\""))
            {
                _failed = true;
                throw TransientError();
            }

            return ValueTask.FromResult(result);
        }
    }

    // El commit SÍ llega a la base, pero el cliente recibe un error transitorio: commit "en duda".
    private sealed class FailAfterFirstCommit : DbTransactionInterceptor
    {
        private bool _failed;

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (!_failed)
            {
                _failed = true;
                throw TransientError();
            }

            return Task.CompletedTask;
        }
    }

    private static async Task<int> MovementCountAsync(Guid itemId)
    {
        await using var owner = OwnerContext();
        return await owner.InventoryMovements.CountAsync(m => m.InventoryItemId == itemId);
    }

    [PostgresFact]
    public async Task FalloTransitorioAntesDelCommit_SeReintenta_SinDuplicarMovimientos()
    {
        // Sin descartar lo que añadió el intento fallido, EF insertaría en el reintento 4 filas en vez de 2.
        Seed? seed = null;
        try
        {
            seed = await SeedAsync(initialStock: 10);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            await using var db = AppContext(tenant, new FailFirstMovementInsert());

            await Service(db).TransferAsync(Transfer(seed, 3), Guid.NewGuid());

            Assert.Equal(3, await MovementCountAsync(seed.ItemId)); // entrada inicial + salida + entrada
            Assert.Equal(7, await BalanceAsync(seed.ItemId, seed.MainId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }

    [PostgresFact]
    public async Task CommitEnDuda_VerifySucceeded_NoRepiteElTraspaso()
    {
        Seed? seed = null;
        try
        {
            seed = await SeedAsync(initialStock: 10);
            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);
            await using var db = AppContext(tenant, new FailAfterFirstCommit());

            await Service(db).TransferAsync(Transfer(seed, 3), Guid.NewGuid());

            Assert.Equal(3, await MovementCountAsync(seed.ItemId));
            Assert.Equal(7, await BalanceAsync(seed.ItemId, seed.MainId));
        }
        finally
        {
            await CleanupAsync(seed);
        }
    }
}
