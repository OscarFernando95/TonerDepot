using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Modelo específico de una marca. Los umbrales de mantenimiento ya no dependen del AssetType
// (Impresora/ComputoEquipo) sino de la marca+modelo concretos del equipo, editables a demanda.
public class AssetModel : BaseEntity
{
    public Guid AssetBrandId { get; set; }
    public AssetBrand AssetBrand { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    // Mantenimiento general: híbrido, lo que ocurra primero.
    public int GeneralPrintThreshold { get; set; } = 30000;
    public int GeneralMonthsInterval { get; set; } = 6;

    // Mantenimiento de unidades: híbrido, lo que ocurra primero (independiente del general).
    public int UnitsPrintThreshold { get; set; } = 30000;
    public int UnitsMonthsInterval { get; set; } = 6;

    // Cambio de insumos: puro contador.
    public int ConsumablesPrintThreshold { get; set; } = 60000;

    // Fin de la vida útil en alquiler: contador total del equipo (la última lectura) en el que debe salir de alquiler y pasar
    // a venta. null = el modelo no tiene límite definido. Es informativo: nada se bloquea ni se cambia solo.
    public int? RentalRetirementPrintThreshold { get; set; }

    // Cuántas impresiones antes del límite se empieza a avisar "próxima a salir de alquiler".
    public int RentalRetirementWarningPrints { get; set; } = 50000;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
