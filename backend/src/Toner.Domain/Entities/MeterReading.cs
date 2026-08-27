using Toner.Domain.Common;

namespace Toner.Domain.Entities;

public class MeterReading : BaseEntity
{
    public Guid AssetId { get; set; }

    // Denormalizado para que la política RLS compare una columna propia en vez de resolver un EXISTS
    // a través de Assets → ClientLocations (SECURITY_AUDIT_V2.md fase 3b).
    //
    // CAPTURA AL ESCRIBIR desde Asset.ClientId: la lectura mide el consumo DE ESE CLIENTE en ese
    // momento. Si el activo se muda a otro cliente, las lecturas viejas siguen siendo del cliente
    // viejo — es dato con relevancia de facturación. Derivarlo del activo en cada consulta
    // reintroduciría el bug que evita esta captura.
    //
    // Nullable: hoy se puede registrar una lectura de un activo en bodega (AddMeterReadingAsync solo
    // comprueba que el activo exista, no su LifecycleStatus). Esa lectura no pertenece a ningún
    // cliente y la política no la muestra a nadie — fail-closed, igual que AssetStatusLog.
    public Guid? ClientId { get; set; }
    public Asset Asset { get; set; } = null!;

    public DateTime ReadingDate { get; set; } = DateTime.UtcNow;
    public long CounterValue { get; set; }

    public Guid? RegisteredByUserId { get; set; }
    public User? RegisteredByUser { get; set; }
}
