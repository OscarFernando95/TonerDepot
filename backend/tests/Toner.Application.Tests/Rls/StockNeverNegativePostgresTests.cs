using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Toner.Application.Common;
using Toner.Application.Common.Exceptions;
using Toner.Application.Common.Interfaces;
using Toner.Application.Inventory.Dtos;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Rls;

// AUDIT.md H-41: el inventario nunca queda en negativo. Contra el Postgres real, con el rol de la aplicación y el
// mismo retry que producción: dos técnicos cierran a la vez visitas que consumen la ÚLTIMA unidad de la misma pieza
// en la misma bodega. Sin el lock ambos la descuentan (saldo -1); con él, el segundo recibe un 409 y su visita
// sigue abierta.
[Collection(nameof(RlsFixtureCollection))]
public class StockNeverNegativePostgresTests
{
    private static readonly Guid LocationA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private readonly RlsFixture _fixture;

    public StockNeverNegativePostgresTests(RlsFixture fixture) => _fixture = fixture;

    private static TonerDbContext OwnerContext() =>
        new(new DbContextOptionsBuilder<TonerDbContext>().UseNpgsql(PostgresFactAttribute.OwnerConnectionString).Options);

    private static TonerDbContext AppContext(TenantContextAccessor tenant, params IInterceptor[] extra) =>
        new(new DbContextOptionsBuilder<TonerDbContext>()
            .UseNpgsql(PostgresFactAttribute.ConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(50), errorCodesToAdd: null))
            .AddInterceptors(new IInterceptor[] { new TenantContextInterceptor(tenant) }.Concat(extra))
            .Options);

    // Pausa el cierre en el que está instalado justo DESPUÉS de leer el saldo de las piezas y lanza el otro cierre,
    // esperándolo como máximo `wait` (con el lock, el otro queda esperando: la espera acotada evita el deadlock).
    private sealed class PauseAfterBalanceRead : DbCommandInterceptor
    {
        private readonly Func<Task> _startOther;
        private bool _fired;
        public Task? Other { get; private set; }

        public PauseAfterBalanceRead(Func<Task> startOther) => _startOther = startOther;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!_fired && command.CommandText.Contains("sum(", StringComparison.OrdinalIgnoreCase)
                        && command.CommandText.Contains("\"InventoryMovements\""))
            {
                _fired = true;
                Other = _startOther();
                await Task.WhenAny(Other, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
            }

            return result;
        }
    }

    [PostgresFact]
    public async Task DosCierresSimultaneos_ConsumenLaUltimaUnidad_SoloUnoPasa_YElSaldoQuedaEnCero()
    {
        await _fixture.EnsureSeededAsync();
        var marker = "PG-H41-" + Guid.NewGuid().ToString("N")[..8];
        var userIds = new List<Guid>();
        var techIds = new List<Guid>();
        var ticketIds = new List<Guid>();
        Guid itemId = Guid.Empty;
        try
        {
            Guid mainId;
            await using (var owner = OwnerContext())
            {
                // El municipio del fixture no tiene zona: el consumo sale de la bodega principal.
                mainId = (await owner.InventoryLocations.FirstAsync(l => l.Kind == InventoryLocationKind.Principal)).Id;
                var role = await owner.Roles.FirstAsync(r => r.Name == RoleNames.Tecnico);
                var item = new InventoryItem { Name = marker, Category = InventoryCategory.Repuesto };
                owner.Add(item);
                itemId = item.Id;
                for (var i = 0; i < 2; i++)
                {
                    var user = TestEntities.User(role);
                    var tech = TestEntities.Technician(user, isActive: true, status: TechnicianStatus.Disponible);
                    var ticket = new ServiceTicket
                    {
                        ClientLocationId = LocationA, ClientId = RlsFixture.ClientA, AssetId = RlsFixture.AssetOfClientA,
                        ReportedByUserId = user.Id, Description = marker, Status = ServiceTicketStatus.Asignado,
                        Priority = ServiceTicketPriority.Media, TechnicianId = tech.Id
                    };
                    owner.AddRange(user, tech, ticket);
                    userIds.Add(user.Id);
                    techIds.Add(tech.Id);
                    ticketIds.Add(ticket.Id);
                }
                owner.InventoryMovements.Add(new InventoryMovement
                {
                    InventoryItemId = item.Id, InventoryLocationId = mainId, Type = InventoryMovementType.Entrada, Delta = 1
                });
                await owner.SaveChangesAsync();
            }

            var tenant = new TenantContextAccessor();
            using var scope = tenant.Push(() => TenantContext.Staff);

            for (var i = 0; i < 2; i++)
            {
                await using var db = AppContext(tenant);
                await TestCheckIn.Create(db, stockLock: new PostgresStockLock(db))
                    .CheckInAsync(techIds[i], new CheckInRequest { ServiceTicketId = ticketIds[i] });
            }

            CheckOutRequest UsingTheLastUnit() => new()
            {
                Resolved = true,
                Parts = new[] { new UsedPartRequest { ItemId = itemId, Quantity = 1 } }
            };

            async Task CloseA()
            {
                await using var dbA = AppContext(tenant);
                await TestCheckIn.Create(dbA, stockLock: new PostgresStockLock(dbA)).CheckOutAsync(techIds[0], UsingTheLastUnit());
            }

            var pause = new PauseAfterBalanceRead(CloseA);
            await using var dbB = AppContext(tenant, pause);
            var b = await Record.ExceptionAsync(() =>
                TestCheckIn.Create(dbB, stockLock: new PostgresStockLock(dbB)).CheckOutAsync(techIds[1], UsingTheLastUnit()));
            var a = await Record.ExceptionAsync(() => pause.Other!);

            var outcomes = new[] { a, b };
            Assert.Single(outcomes, e => e is null);
            var rejected = Assert.Single(outcomes, e => e is ConflictException);
            Assert.Contains("Stock insuficiente", rejected!.Message);

            await using var check = OwnerContext();
            Assert.Equal(0, await check.InventoryMovements.Where(m => m.InventoryItemId == itemId).SumAsync(m => m.Delta));
            // El cierre rechazado no cerró nada: exactamente una de las dos visitas sigue abierta.
            Assert.Equal(1, await check.TimeLogs.CountAsync(t => techIds.Contains(t.TechnicianId) && t.EndTime == null));
        }
        finally
        {
            await using var cleanup = new NpgsqlConnection(PostgresFactAttribute.OwnerConnectionString);
            await cleanup.OpenAsync();
            async Task Exec(string sql, object value)
            {
                await using var cmd = new NpgsqlCommand(sql, cleanup);
                cmd.Parameters.AddWithValue("v", value);
                await cmd.ExecuteNonQueryAsync();
            }
            await Exec(@"DELETE FROM ""InventoryMovements"" WHERE ""InventoryItemId"" = @v", itemId);
            await Exec(@"DELETE FROM ""TimeLogs"" WHERE ""TechnicianId"" = ANY(@v)", techIds.ToArray());
            await Exec(@"DELETE FROM ""TechnicianAvailabilities"" WHERE ""TechnicianId"" = ANY(@v)", techIds.ToArray());
            await Exec(@"DELETE FROM ""ServiceTickets"" WHERE ""Id"" = ANY(@v)", ticketIds.ToArray());
            await Exec(@"DELETE FROM ""Technicians"" WHERE ""Id"" = ANY(@v)", techIds.ToArray());
            await Exec(@"DELETE FROM ""Users"" WHERE ""Id"" = ANY(@v)", userIds.ToArray());
            await Exec(@"DELETE FROM ""InventoryItems"" WHERE ""Id"" = @v", itemId);
        }
    }
}
