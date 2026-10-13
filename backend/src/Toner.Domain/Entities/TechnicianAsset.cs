using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Activos vinculados a un técnico: gobierna qué ve en "Lectura de contadores" y en qué máquinas puede
// registrar tóner. Hay dos orígenes (ver Source): el administrador lo vincula a mano por activo individual
// (decisión del 2026-09-27), o el sistema lo deriva de la zona del técnico (decisión del 2026-10-11:
// los activos Instalado de su zona quedan vinculados solos). Como el vínculo concede permisos, el
// automático también se retira solo cuando deja de aplicar — nunca el manual.
public class TechnicianAsset : BaseEntity
{
    public Guid TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public TechnicianAssetSource Source { get; set; } = TechnicianAssetSource.Manual;
}
