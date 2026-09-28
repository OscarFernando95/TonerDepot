namespace Toner.Application.Technicians.Dtos;

public class CheckOutRequest
{
    // true (default): el trabajo quedó terminado, se marca Resuelto/Completada.
    // false: el técnico solo se libera (pausa, fin de turno, etc.), el ticket/orden sigue EnProceso.
    public bool Resolved { get; set; } = true;

    public string? Notes { get; set; }

    // Ubicación del técnico al cerrar (ver CheckInRequest).
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }

    // Foto "después" ya subida. Obligatoria cuando se resuelve un ticket u orden (Resolved = true).
    public Guid? AfterEvidenceId { get; set; }

    // InitialCounterValue/InitialCounterDate: el contador registrado al cerrar la visita. Obligatorios
    // cuando se cierra una instalación de activo o una orden de mantenimiento (Resolved = true) — es el
    // dato que mantiene actualizado el cronograma. Opcionales al cerrar un ticket de soporte (no toda
    // visita correctiva implica estar frente al contador).
    public string? Area { get; set; }
    public long? InitialCounterValue { get; set; }
    public DateTime? InitialCounterDate { get; set; }

    // Obligatorios solo en la rama de instalación. UnitsMaintenanceDone e "insumos nuevos" son mutuamente
    // excluyentes: false implica insumos nuevos (offset 0); true implica que se conoce cuánto uso ya
    // traen los insumos instalados en esas unidades (ExistingConsumablesPrints).
    public bool? GeneralMaintenanceDone { get; set; }
    public bool? UnitsMaintenanceDone { get; set; }
    public long? ExistingConsumablesPrints { get; set; }

    // Totalmente opcionales: solo tienen efecto al cerrar un ticket que no tiene Asset asociado (cliente
    // externo con equipo no catalogado) — le permiten al técnico dejar constancia de qué equipo era.
    public string? ExternalAssetBrand { get; set; }
    public string? ExternalAssetModel { get; set; }
    public long? ExternalAssetCounter { get; set; }
}
