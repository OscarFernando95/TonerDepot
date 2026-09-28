using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Toner.Application.Common.Exceptions;
using Toner.Application.Evidences;
using Toner.Application.Geo;
using Toner.Application.Tests.TestSupport;
using Toner.Application.Technicians.Dtos;
using Toner.Domain.Common;
using Toner.Domain.Entities;
using Toner.Domain.Enums;
using Toner.Infrastructure.Persistence;

namespace Toner.Application.Tests.Evidence;

// Foto obligatoria en check-in/out, ubicación registrada contra la sede, y validación de las imágenes subidas.
public class EvidenceAndLocationTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1 };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

    // Sede en el centro de Bogotá.
    private const double SiteLat = 4.5981;
    private const double SiteLon = -74.0760;

    private sealed record Scenario(string DbName, Guid TechnicianId, Guid TicketId, Guid TechnicianUserId, Guid ClientId);

    private static async Task<Scenario> SeedAsync(bool siteHasCoordinates = true, Guid? assignedTo = null)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = TonerTestDb.CreateContext(dbName);
        var role = TestEntities.Role(RoleNames.Cliente);
        var user = TestEntities.User(role);
        var city = TestEntities.City();
        var client = TestEntities.Client();
        var location = TestEntities.ClientLocation(client, city);
        if (siteHasCoordinates)
        {
            location.Latitude = SiteLat;
            location.Longitude = SiteLon;
        }

        var techRole = TestEntities.Role(RoleNames.Tecnico);
        var techUser = TestEntities.User(techRole);
        var technician = TestEntities.Technician(techUser);
        var ticket = TestEntities.ServiceTicket(location, user, ServiceTicketStatus.Asignado, technicianId: assignedTo ?? technician.Id);
        ticket.ClientId = client.Id;

        db.AddRange(role, user, city, client, location, techRole, techUser, technician, ticket);
        await db.SaveChangesAsync();
        return new Scenario(dbName, technician.Id, ticket.Id, techUser.Id, client.Id);
    }

    private static EvidenceService BuildEvidence(TonerDbContext db, InMemoryEvidenceStorage storage, long maxBytes = 10 * 1024 * 1024) =>
        new(db, storage, Options.Create(new EvidenceOptions { MaxBytes = maxBytes }), NullLogger<EvidenceService>.Instance);

    private static async Task<Guid> UploadAsync(Scenario s, EvidenceKind kind, InMemoryEvidenceStorage? storage = null, byte[]? bytes = null)
    {
        using var db = TonerTestDb.CreateContext(s.DbName);
        var dto = await BuildEvidence(db, storage ?? new InMemoryEvidenceStorage())
            .UploadAsync(s.TechnicianId, kind, s.TicketId, null, new MemoryStream(bytes ?? Jpeg));
        return dto.Id;
    }

    // ── Geolocalización pura ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void GeoDistance_UnaCentesimaDeGradoDeLatitud_SonAproximadamente111Metros()
    {
        var meters = GeoDistance.Meters(SiteLat, SiteLon, SiteLat + 0.001, SiteLon);

        Assert.InRange(meters, 110, 112);
    }

    [Theory]
    [InlineData(SiteLat + 0.001, SiteLon, LocationStatus.EnSitio)]       // ~111 m, dentro de 250 m
    [InlineData(SiteLat + 0.05, SiteLon, LocationStatus.FueraDeSitio)]   // ~5,5 km
    public void LocationEvaluator_ComparaContraElRadioDeLaSede(double lat, double lon, LocationStatus expected)
    {
        var result = LocationEvaluator.Evaluate(SiteLat, SiteLon, lat, lon, 250);

        Assert.Equal(expected, result.Status);
        Assert.NotNull(result.DistanceMeters);
    }

    [Fact]
    public void LocationEvaluator_SinUbicacionOSedeSinCoordenadas_SonEstadosNoErrores()
    {
        Assert.Equal(LocationStatus.SinUbicacion, LocationEvaluator.Evaluate(SiteLat, SiteLon, null, null, 250).Status);
        Assert.Equal(LocationStatus.SinUbicacion, LocationEvaluator.Evaluate(SiteLat, SiteLon, 999, 999, 250).Status);
        Assert.Equal(LocationStatus.SedeSinCoordenadas, LocationEvaluator.Evaluate(null, null, SiteLat, SiteLon, 250).Status);
    }

    // ── Firma de imagen ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public void ImageSignature_ReconoceJpegPngWebp_YRechazaElRestoAunqueSeaHtml()
    {
        Assert.Equal("image/jpeg", ImageSignature.Detect(Jpeg)!.ContentType);
        Assert.Equal("image/png", ImageSignature.Detect(Png)!.ContentType);
        var webp = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };
        Assert.Equal("image/webp", ImageSignature.Detect(webp)!.ContentType);
        Assert.Null(ImageSignature.Detect("<html><script>alert(1)</script></html>"u8));
        Assert.Null(ImageSignature.Detect(Array.Empty<byte>()));
    }

    // ── Subida ───────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Upload_GuardaElBlobConClaveDelClienteYNombreGenerado()
    {
        var s = await SeedAsync();
        var storage = new InMemoryEvidenceStorage();

        var id = await UploadAsync(s, EvidenceKind.Antes, storage);

        var key = Assert.Single(storage.Blobs.Keys);
        Assert.StartsWith($"{s.ClientId}/", key);
        Assert.Equal("image/jpeg", storage.Blobs[key].ContentType);
        using var db = TonerTestDb.CreateContext(s.DbName);
        var stored = await db.Evidences.SingleAsync(e => e.Id == id);
        Assert.Equal(s.ClientId, stored.ClientId);
        Assert.Equal(EvidenceKind.Antes, stored.Kind);
        Assert.DoesNotContain("..", stored.FileName);
    }

    [Fact]
    public async Task Upload_ArchivoQueNoEsImagen_SeRechazaYNoSeGuardaNada()
    {
        var s = await SeedAsync();
        var storage = new InMemoryEvidenceStorage();

        await Assert.ThrowsAsync<ValidationException>(() => UploadAsync(s, EvidenceKind.Antes, storage, "<html></html>"u8.ToArray()));

        Assert.Empty(storage.Blobs);
    }

    [Fact]
    public async Task Upload_MasGrandeQueElMaximo_SeRechaza()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);
        var big = new byte[2048];
        Jpeg.CopyTo(big, 0);

        await Assert.ThrowsAsync<ValidationException>(() =>
            BuildEvidence(db, new InMemoryEvidenceStorage(), maxBytes: 1024).UploadAsync(s.TechnicianId, EvidenceKind.Antes, s.TicketId, null, new MemoryStream(big)));
    }

    [Fact]
    public async Task Upload_TicketDeOtroTecnico_EsProhibido()
    {
        var s = await SeedAsync(assignedTo: Guid.NewGuid());
        using var db = TonerTestDb.CreateContext(s.DbName);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            BuildEvidence(db, new InMemoryEvidenceStorage()).UploadAsync(s.TechnicianId, EvidenceKind.Antes, s.TicketId, null, new MemoryStream(Jpeg)));
    }

    [Fact]
    public async Task GetContent_OtroTecnicoNoPuedeVerLaFoto_PeroElStaffSi()
    {
        var s = await SeedAsync();
        var storage = new InMemoryEvidenceStorage();
        var id = await UploadAsync(s, EvidenceKind.Antes, storage);
        using var db = TonerTestDb.CreateContext(s.DbName);
        var service = BuildEvidence(db, storage);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.GetContentAsync(id, requesterTechnicianId: Guid.NewGuid()));
        Assert.Equal("image/jpeg", (await service.GetContentAsync(id, requesterTechnicianId: null)).ContentType);
        Assert.Equal("image/jpeg", (await service.GetContentAsync(id, requesterTechnicianId: s.TechnicianId)).ContentType);
    }

    // ── Check-in con fotos y ubicación ───────────────────────────────────────────────────────────
    [Fact]
    public async Task CheckIn_SinFotoAntes_SeRechazaCuandoLaPoliticaLaExige()
    {
        var s = await SeedAsync();
        using var db = TonerTestDb.CreateContext(s.DbName);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            TestCheckIn.Create(db, requirePhotos: true).CheckInAsync(s.TechnicianId, new CheckInRequest { ServiceTicketId = s.TicketId }));

        Assert.Contains("foto", ex.Message);
        Assert.Empty(await db.TimeLogs.ToListAsync());
    }

    [Fact]
    public async Task CheckIn_ConFotoAntes_AtaLaFotoAlRegistroYGuardaLaUbicacion()
    {
        var s = await SeedAsync();
        var photoId = await UploadAsync(s, EvidenceKind.Antes);
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db, requirePhotos: true).CheckInAsync(s.TechnicianId, new CheckInRequest
            {
                ServiceTicketId = s.TicketId,
                BeforeEvidenceId = photoId,
                Latitude = SiteLat + 0.001,
                Longitude = SiteLon,
                AccuracyMeters = 12
            });
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        var log = await check.TimeLogs.SingleAsync();
        Assert.Equal(LocationStatus.EnSitio, log.CheckInLocationStatus);
        Assert.InRange(log.CheckInDistanceMeters!.Value, 100, 120);
        Assert.Equal(12, log.CheckInAccuracyMeters);
        Assert.Equal(log.Id, (await check.Evidences.SingleAsync()).TimeLogId);
    }

    [Fact]
    public async Task CheckIn_LejosDeLaSede_NoBloquea_PeroQuedaMarcadoFueraDeSitio()
    {
        var s = await SeedAsync();
        var photoId = await UploadAsync(s, EvidenceKind.Antes);
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db, requirePhotos: true).CheckInAsync(s.TechnicianId, new CheckInRequest
            {
                ServiceTicketId = s.TicketId, BeforeEvidenceId = photoId, Latitude = SiteLat + 0.2, Longitude = SiteLon
            });
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        Assert.Equal(LocationStatus.FueraDeSitio, (await check.TimeLogs.SingleAsync()).CheckInLocationStatus);
    }

    [Theory]
    [InlineData(true, false, LocationStatus.SinUbicacion)]
    [InlineData(false, true, LocationStatus.SedeSinCoordenadas)]
    public async Task CheckIn_SinUbicacionOSedeSinCoordenadas_QuedaRegistradoElMotivo(bool siteHasCoordinates, bool sendsLocation, LocationStatus expected)
    {
        var s = await SeedAsync(siteHasCoordinates);

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db, requirePhotos: false).CheckInAsync(s.TechnicianId, new CheckInRequest
            {
                ServiceTicketId = s.TicketId,
                Latitude = sendsLocation ? SiteLat : null,
                Longitude = sendsLocation ? SiteLon : null
            });
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        Assert.Equal(expected, (await check.TimeLogs.SingleAsync()).CheckInLocationStatus);
    }

    [Fact]
    public async Task CheckIn_ConFotoDeTipoDespues_OYaUsada_SeRechaza()
    {
        var s = await SeedAsync();
        var wrongKind = await UploadAsync(s, EvidenceKind.Despues);
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                TestCheckIn.Create(db, requirePhotos: true).CheckInAsync(s.TechnicianId, new CheckInRequest { ServiceTicketId = s.TicketId, BeforeEvidenceId = wrongKind }));
        }

        var photoId = await UploadAsync(s, EvidenceKind.Antes);
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db, requirePhotos: true).CheckInAsync(s.TechnicianId, new CheckInRequest { ServiceTicketId = s.TicketId, BeforeEvidenceId = photoId });
            await TestCheckIn.Create(db).CheckOutAsync(s.TechnicianId, new CheckOutRequest { Resolved = false });
        }

        // Reutilizar la misma foto en una segunda visita no se permite.
        using var db2 = TonerTestDb.CreateContext(s.DbName);
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            TestCheckIn.Create(db2, requirePhotos: true).CheckInAsync(s.TechnicianId, new CheckInRequest { ServiceTicketId = s.TicketId, BeforeEvidenceId = photoId }));
        Assert.Contains("ya se usó", ex.Message);
    }

    // ── Check-out ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CheckOut_ResolverSinFotoDespues_SeRechaza_PeroPausarNoLaExige()
    {
        var s = await SeedAsync();
        var before = await UploadAsync(s, EvidenceKind.Antes);
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db, requirePhotos: true).CheckInAsync(s.TechnicianId, new CheckInRequest { ServiceTicketId = s.TicketId, BeforeEvidenceId = before });
        }

        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await Assert.ThrowsAsync<ConflictException>(() =>
                TestCheckIn.Create(db, requirePhotos: true).CheckOutAsync(s.TechnicianId, new CheckOutRequest { Resolved = true }));
        }

        var after = await UploadAsync(s, EvidenceKind.Despues);
        using (var db = TonerTestDb.CreateContext(s.DbName))
        {
            await TestCheckIn.Create(db, requirePhotos: true).CheckOutAsync(s.TechnicianId, new CheckOutRequest
            {
                Resolved = true, AfterEvidenceId = after, Latitude = SiteLat, Longitude = SiteLon
            });
        }

        using var check = TonerTestDb.CreateContext(s.DbName);
        var log = await check.TimeLogs.SingleAsync();
        Assert.Equal(LocationStatus.EnSitio, log.CheckOutLocationStatus);
        Assert.All(await check.Evidences.ToListAsync(), e => Assert.Equal(log.Id, e.TimeLogId));
    }
}
