using Toner.Application.Push;
using Toner.Domain.Enums;
using Toner.Infrastructure.Push;

namespace Toner.Application.Tests.Push;

public class PushDetailTests
{
    private static PushMessage Generic(string eventName) => new("Título", "Texto genérico.",
        new Dictionary<string, string> { ["type"] = "ticket", ["id"] = Guid.NewGuid().ToString(), ["event"] = eventName });

    [Fact]
    public void Ticket_LlevaClienteCiudadCriticidadEquipoYArea()
    {
        var detail = new PushDetail("Acme", "Bogotá", ServiceTicketPriority.Critica, PushDetail.JoinEquipment("Ricoh", "MP 2555"), "Contabilidad");

        Assert.Equal("Acme · Bogotá\nCriticidad Crítica · Ricoh MP 2555 · Área Contabilidad", detail.Format());
    }

    [Fact]
    public void LoQueNoHay_SeOmite_SinSeparadoresHuerfanos()
    {
        // Orden de mantenimiento o instalación pendiente: sin criticidad; la instalación aún no tiene área.
        Assert.Equal("Acme · Bogotá\nRicoh MP 2555", new PushDetail("Acme", "Bogotá", null, "Ricoh MP 2555", null).Format());
        Assert.Equal("Acme", new PushDetail("Acme", null, null, null, "  ").Format());
        Assert.Equal("", new PushDetail(null, null, null, null, null).Format());
    }

    [Theory]
    [InlineData(ServiceTicketPriority.Baja, "Baja")]
    [InlineData(ServiceTicketPriority.Media, "Media")]
    [InlineData(ServiceTicketPriority.Alta, "Alta")]
    [InlineData(ServiceTicketPriority.Critica, "Crítica")]
    public void Criticidad_SeMuestraEnEspanolConTilde(ServiceTicketPriority priority, string label) =>
        Assert.Equal(label, PushDetail.PriorityLabel(priority));

    [Theory]
    [InlineData(PushChangeInterceptor.EventAssigned)]
    [InlineData(PushChangeInterceptor.EventUnassigned)]
    [InlineData(PushChangeInterceptor.EventAvailable)]
    public void EnLosAvisosDeTrabajo_ElResumenReemplazaAlTextoGenerico(string eventName)
    {
        var message = new PushDetail("Acme", "Bogotá", null, null, null).ApplyTo(Generic(eventName));

        Assert.Equal("Acme · Bogotá", message.Body);
        Assert.Equal("Título", message.Title);
    }

    [Theory]
    [InlineData(PushChangeInterceptor.EventRemoved)]
    [InlineData(PushChangeInterceptor.EventCancelled)]
    public void EnLosAvisosDeServicioQueYaNoEsMio_ElResumenAcompanaALaFrase(string eventName)
    {
        var message = new PushDetail("Acme", "Bogotá", null, null, null).ApplyTo(Generic(eventName));

        Assert.Equal("Texto genérico.\nAcme · Bogotá", message.Body);
    }

    [Fact]
    public void ElResumenNuncaTocaLosDatos_SoloIds()
    {
        var original = Generic(PushChangeInterceptor.EventAssigned);

        var enriched = new PushDetail("Acme", "Bogotá", ServiceTicketPriority.Alta, "Ricoh X", "A1").ApplyTo(original);

        Assert.Same(original.Data, enriched.Data);
    }

    [Fact]
    public void SinResumen_ElMensajeQuedaIgual()
    {
        var original = Generic(PushChangeInterceptor.EventAssigned);

        Assert.Same(original, new PushDetail(null, null, null, null, null).ApplyTo(original));
    }
}
