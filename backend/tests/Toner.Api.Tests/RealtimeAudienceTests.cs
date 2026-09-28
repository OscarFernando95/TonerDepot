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
            groups);
    }

    [Theory]
    [InlineData("Visit")]
    [InlineData("Technician")]
    [InlineData("Evidence")]
    [InlineData("Schedule")]
    [InlineData("User")]
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

        Assert.Equal(new[] { RealtimeGroups.Staff }, groups);
    }
}
