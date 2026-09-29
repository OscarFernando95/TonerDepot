using Toner.Domain.Common;

namespace Toner.Domain.Entities;

// Activos vinculados explícitamente a un técnico: gobierna qué ve en "Lectura de contadores" (app
// móvil). A diferencia de TechnicianCoverage (cobertura por ciudad, usada para instalaciones
// pendientes), esta vinculación es por activo individual y no se infiere de la cobertura — el
// administrador debe vincular cada activo a mano. Ver decisión de arquitectura del 2026-09-27:
// "vinculación explícita obligatoria, por activo individual".
public class TechnicianAsset : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
}
