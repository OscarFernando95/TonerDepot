using Microsoft.EntityFrameworkCore;
using Toner.Application.Assets;
using Toner.Application.Assignment;
using Toner.Application.Common.Exceptions;
using Toner.Application.Maintenance;
using Toner.Application.Technicians;
using Toner.Application.Technicians.Dtos;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Tickets;
using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Application.Tests.Technicians;

public class TechnicianCheckInServiceTests
{
    private static TechnicianCheckInService BuildService(Infrastructure.Persistence.TonerDbContext db) => TestCheckIn.Create(db);

    [Fact]
    public async Task CheckInAsync_TicketAssignedToTechnician_MovesTicketAndTechnicianToInProgress()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var status = await service.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });

        Assert.Equal(nameof(TechnicianStatus.Ocupado), status.Status);
        Assert.Equal(ticket.Id, status.ActiveServiceTicketId);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(ServiceTicketStatus.EnProceso, updatedTicket.Status);
        var updatedTechnician = await assertDb.Technicians.SingleAsync(t => t.Id == technician.Id);
        Assert.Equal(TechnicianStatus.Ocupado, updatedTechnician.Status);
    }

    [Fact]
    public async Task CheckInAsync_TechnicianAlreadyOcupado_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Ocupado);

        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id }));
    }

    [Fact]
    public async Task CheckInAsync_TicketAssignedToAnotherTechnician_ThrowsForbidden()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var ownerUser = TestEntities.User(techRole);
        var ownerTechnician = TestEntities.Technician(ownerUser);
        var otherUser = TestEntities.User(techRole);
        var otherTechnician = TestEntities.Technician(otherUser);

        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: ownerTechnician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, ownerUser, ownerTechnician, otherUser, otherTechnician, ticket);
        await arrangeDb.SaveChangesAsync();

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.CheckInAsync(otherTechnician.Id, new CheckInRequest { ServiceTicketId = ticket.Id }));
    }

    [Fact]
    public async Task CheckOutAsync_Resolved_MarksTicketResueltoAndFreesUpTechnician()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var status = await service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = true });

        Assert.Equal(nameof(TechnicianStatus.Disponible), status.Status);
        Assert.Null(status.ActiveServiceTicketId);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(ServiceTicketStatus.Resuelto, updatedTicket.Status);
        Assert.NotNull(updatedTicket.ResolvedAt);
    }

    [Fact]
    public async Task CheckOutAsync_TicketWithoutAsset_SavesOptionalExternalDeviceFields()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        // Sin AssetId: cliente externo, equipo no catalogado.
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.CheckOutAsync(technician.Id, new CheckOutRequest
        {
            Resolved = true,
            ExternalAssetBrand = "Epson",
            ExternalAssetModel = "L3250",
            ExternalAssetCounter = 4200
        });

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal("Epson", updatedTicket.ExternalAssetBrand);
        Assert.Equal("L3250", updatedTicket.ExternalAssetModel);
        Assert.Equal(4200, updatedTicket.ExternalAssetCounter);
    }

    [Fact]
    public async Task CheckOutAsync_TicketWithoutAsset_ExternalDeviceFieldsRemainNullWhenNotProvided()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = true });

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Null(updatedTicket.ExternalAssetBrand);
        Assert.Null(updatedTicket.ExternalAssetModel);
        Assert.Null(updatedTicket.ExternalAssetCounter);
    }

    [Fact]
    public async Task CheckOutAsync_NotResolved_LeavesTicketInProgressButFreesUpTechnician()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: technician.Id);

        arrangeDb.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { ServiceTicketId = ticket.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var status = await service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = false, Notes = "Falta repuesto" });

        Assert.Equal(nameof(TechnicianStatus.Disponible), status.Status);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedTicket = await assertDb.ServiceTickets.SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(ServiceTicketStatus.EnProceso, updatedTicket.Status);
        Assert.Null(updatedTicket.ResolvedAt);
    }

    [Fact]
    public async Task CheckOutAsync_MaintenanceOrder_Resolved_CompletesOrderAndRecalculatesSchedule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var client = TestEntities.Client();
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model);
        var schedule = TestEntities.MaintenanceSchedule(asset, contract);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        var order = TestEntities.MaintenanceOrder(schedule, asset, MaintenanceOrderStatus.Asignada, technician.Id);

        arrangeDb.AddRange(client, contract, brand, model, asset, schedule, techRole, techUser, technician, order);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { MaintenanceOrderId = order.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = true, InitialCounterValue = 12000 });

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedOrder = await assertDb.MaintenanceOrders.SingleAsync(o => o.Id == order.Id);
        Assert.Equal(MaintenanceOrderStatus.Completada, updatedOrder.Status);
        var updatedSchedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.Id == schedule.Id);
        Assert.True(updatedSchedule.NextGeneralDueAt > DateTime.UtcNow.AddMonths(5));
        Assert.NotNull(updatedSchedule.LastGeneralMaintenanceAt);
        Assert.Equal(12000, updatedSchedule.LastGeneralMaintenanceCounter);
    }

    [Fact]
    public async Task CheckOutAsync_AssetInstallation_Resolved_InstallsAssetAndRecordsInitialReading()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        arrangeDb.AddRange(city, client, location, brand, model, asset, techRole, techUser, technician);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { AssetId = asset.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        var status = await service.CheckOutAsync(technician.Id, new CheckOutRequest
        {
            Resolved = true,
            Area = "Recepción",
            InitialCounterValue = 1200,
            GeneralMaintenanceDone = true,
            UnitsMaintenanceDone = false
        });

        Assert.Equal(nameof(TechnicianStatus.Disponible), status.Status);
        Assert.Null(status.ActiveAssetInstallationId);

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var updatedAsset = await assertDb.Assets.SingleAsync(a => a.Id == asset.Id);
        Assert.Equal(AssetLifecycleStatus.Instalado, updatedAsset.LifecycleStatus);
        Assert.Equal("Recepción", updatedAsset.Area);
        var reading = await assertDb.MeterReadings.SingleAsync(m => m.AssetId == asset.Id);
        Assert.Equal(1200, reading.CounterValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckOutAsync_AssetInstallation_Resolved_LinksAssetToTechnicianExactlyOnce(bool alreadyLinked)
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        arrangeDb.AddRange(city, client, location, brand, model, asset, techRole, techUser, technician);
        if (alreadyLinked)
        {
            arrangeDb.TechnicianAssets.Add(new Domain.Entities.TechnicianAsset { TechnicianId = technician.Id, AssetId = asset.Id });
        }
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            await BuildService(checkInDb).CheckInAsync(technician.Id, new CheckInRequest { AssetId = asset.Id });
        }

        using (var actDb = TonerTestDb.CreateContext(dbName))
        {
            await BuildService(actDb).CheckOutAsync(technician.Id, new CheckOutRequest
            {
                Resolved = true,
                Area = "Recepción",
                InitialCounterValue = 1200,
                GeneralMaintenanceDone = true,
                UnitsMaintenanceDone = false
            });
        }

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var link = await assertDb.TechnicianAssets.SingleAsync(ta => ta.AssetId == asset.Id);
        Assert.Equal(technician.Id, link.TechnicianId);
    }

    [Fact]
    public async Task CheckOutAsync_AssetInstallation_MissingGeneralOrUnitsMaintenanceDone_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        arrangeDb.AddRange(city, client, location, brand, model, asset, techRole, techUser, technician);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { AssetId = asset.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = true, Area = "Recepción", InitialCounterValue = 1000 }));
    }

    [Fact]
    public async Task CheckOutAsync_AssetInstallation_WithActiveContract_UpsertsScheduleWithUnitsAndConsumablesOffset()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var contract = TestEntities.Contract(client);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);
        var link = TestEntities.ContractAsset(contract, asset);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        arrangeDb.AddRange(city, client, location, contract, brand, model, asset, link, techRole, techUser, technician);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { AssetId = asset.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await service.CheckOutAsync(technician.Id, new CheckOutRequest
        {
            Resolved = true,
            Area = "Recepción",
            InitialCounterValue = 1000,
            GeneralMaintenanceDone = true,
            UnitsMaintenanceDone = true,
            ExistingConsumablesPrints = 30000 // insumos con uso previo -> offset explícito de 30.000
        });

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var schedule = await assertDb.MaintenanceSchedules.SingleAsync(s => s.AssetId == asset.Id);
        Assert.Equal(contract.Id, schedule.ContractId);
        Assert.Equal(1000 + 30000, schedule.NextGeneralDueCounter);
        Assert.Equal(1000 + 30000, schedule.NextUnitsDueCounter);
        Assert.Equal(1000 + 30000, schedule.NextConsumablesDueCounter); // 60000 - 30000 de offset
    }

    [Fact]
    public async Task CheckOutAsync_AssetInstallation_MissingInitialCounter_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser, isActive: true, status: TechnicianStatus.Disponible);

        arrangeDb.AddRange(city, client, location, brand, model, asset, techRole, techUser, technician);
        await arrangeDb.SaveChangesAsync();

        using (var checkInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(checkInDb);
            await checkInService.CheckInAsync(technician.Id, new CheckInRequest { AssetId = asset.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CheckOutAsync(technician.Id, new CheckOutRequest { Resolved = true, Area = "Recepción" }));

        using var assertDb = TonerTestDb.CreateContext(dbName);
        var untouchedAsset = await assertDb.Assets.SingleAsync(a => a.Id == asset.Id);
        Assert.Equal(AssetLifecycleStatus.PendienteInstalacion, untouchedAsset.LifecycleStatus);
        var updatedTechnician = await assertDb.Technicians.SingleAsync(t => t.Id == technician.Id);
        Assert.Equal(TechnicianStatus.Ocupado, updatedTechnician.Status);
    }

    [Fact]
    public async Task CheckInAsync_AssetAlreadyBeingInstalledByAnotherTechnician_Throws()
    {
        var dbName = Guid.NewGuid().ToString();
        using var arrangeDb = TonerTestDb.CreateContext(dbName);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        var brand = TestEntities.AssetBrand();
        var model = TestEntities.AssetModel(brand);
        var asset = TestEntities.Asset(model, AssetLifecycleStatus.PendienteInstalacion, location.Id);

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var firstUser = TestEntities.User(techRole);
        var firstTechnician = TestEntities.Technician(firstUser, isActive: true, status: TechnicianStatus.Disponible);
        var secondUser = TestEntities.User(techRole);
        var secondTechnician = TestEntities.Technician(secondUser, isActive: true, status: TechnicianStatus.Disponible);

        arrangeDb.AddRange(city, client, location, brand, model, asset, techRole, firstUser, firstTechnician, secondUser, secondTechnician);
        await arrangeDb.SaveChangesAsync();

        using (var firstCheckInDb = TonerTestDb.CreateContext(dbName))
        {
            var checkInService = BuildService(firstCheckInDb);
            await checkInService.CheckInAsync(firstTechnician.Id, new CheckInRequest { AssetId = asset.Id });
        }

        using var actDb = TonerTestDb.CreateContext(dbName);
        var service = BuildService(actDb);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CheckInAsync(secondTechnician.Id, new CheckInRequest { AssetId = asset.Id }));
    }
}
