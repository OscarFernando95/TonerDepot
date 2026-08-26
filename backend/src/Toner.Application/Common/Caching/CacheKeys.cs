namespace Toner.Application.Common.Caching;

// Claves con namespace para que sean fáciles de razonar y no choquen con nada futuro
// (CODE_QUALITY_AUDIT.md hallazgo #10).
public static class CacheKeys
{
    public const string Cities = "catalog:cities";
    public const string AssetBrands = "catalog:asset-brands";

    // Una entrada por marca, no una sola para todos: crear o editar un modelo de la marca A no debe
    // tumbar la caché de la marca B.
    public static string AssetModels(Guid brandId) => $"catalog:asset-models:{brandId}";

    // ⚠️ El tenant va en la clave A PROPÓSITO, aunque hoy /api/dashboard/summary sea solo-staff
    // ([Authorize(Roles = StaffRoles)]) y por tanto el resultado sea el mismo para todos.
    //
    // El dashboard consulta ServiceTickets y proyecta ClientLocation — DOS tablas con RLS. Que hoy
    // sea seguro cachearlo depende de la AUTORIZACIÓN del endpoint, no del modelo de datos: si
    // alguien abriera este endpoint al rol Cliente, una clave sin tenant convertiría la caché en una
    // fuga entre clientes que evade RLS y el filtrado de C# a la vez. Con el tenant en la clave, ese
    // cambio futuro es seguro por construcción en vez de seguro por casualidad.
    //
    // periodDays debe venir YA NORMALIZADO (ver DashboardService): si no, ?periodDays=0, =-5 y =30
    // crearían tres entradas con contenido idéntico.
    public static string DashboardSummary(bool isStaff, Guid? clientId, int normalizedPeriodDays) =>
        isStaff
            ? $"dashboard:summary:staff:{normalizedPeriodDays}"
            : $"dashboard:summary:client:{clientId}:{normalizedPeriodDays}";
}
