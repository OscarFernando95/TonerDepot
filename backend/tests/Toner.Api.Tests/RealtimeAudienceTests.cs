using Toner.Api.Hubs;
using Toner.Application.Realtime;

namespace Toner.Api.Tests;

// A quién se le avisa de cada cambio: el staff ve todo, un técnico solo lo suyo, un cliente solo lo de su empresa, y
// lo interno (visitas, técnicos, usuarios…) nunca llega a un cliente.
public class RealtimeAudienceTests
{
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly Guid TechA = Guid.NewGuid();
    private static readonly Guid TechB = Guid.NewGuid();

    [Fact]
    public void Ticket_VaAlStaff_AlClienteDueno_YATodosLosTecnicosInvolucrados()
    {
        var groups = RealtimeAudience.GroupsFor(new EntityChange("Ticket", Guid.NewGuid(), "updated", Client, new[] { TechA, TechB }));

        Assert.Equivalent(
            new[] { RealtimeGroups.Staff, RealtimeGroups.Client(Client), RealtimeGroups.Technician(TechA), RealtimeGroups.Technician(TechB) },
            groups, strict: true);
    }

    [Theory]
    [InlineData("Visit")]
    [InlineData("Technician")]
    [InlineData("Evidence")]
    [InlineData("Schedule")]
    [InlineData("User")]
    [InlineData("Holiday")]
    [InlineData("TechnicianAsset")]
    public void EntidadesInternas_NuncaVanAUnCliente(string entity)
    {
        var groups = RealtimeAudience.GroupsFor(new EntityChange(entity, Guid.NewGuid(), "updated", Client, new[] { TechA }));

        Assert.DoesNotContain(RealtimeGroups.Client(Client), groups);
        Assert.Contains(RealtimeGroups.Staff, groups);
    }

    [Fact]
    public void SinClienteValido_NoSeAvisaAGrupoDeCliente()
    {
        var groups = RealtimeAudience.GroupsFor(new EntityChange("Asset", Guid.NewGuid(), "updated", null, Array.Empty<Guid>()));

        Assert.Equivalent(new[] { RealtimeGroups.Staff, RealtimeGroups.AllTechnicians }, groups, strict: true);
        Assert.DoesNotContain(groups, g => g.StartsWith("client:"));
    }

    [Theory]
    [InlineData("Holiday")]
    [InlineData("MeterReading")]
    [InlineData("Asset")]
    public void CosasCompartidasPorElEquipo_VanATodosLosTecnicos(string entity)
    {
        var groups = RealtimeAudience.GroupsFor(new EntityChange(entity, Guid.NewGuid(), "updated", Client, Array.Empty<Guid>()));

        Assert.Contains(RealtimeGroups.AllTechnicians, groups);
    }

    [Fact]
    public void LecturaDeContador_VaAlClienteDueno()
    {
        Assert.Contains(RealtimeGroups.Client(Client),
            RealtimeAudience.GroupsFor(new EntityChange("MeterReading", Guid.NewGuid(), "created", Client, Array.Empty<Guid>())));
    }
}
