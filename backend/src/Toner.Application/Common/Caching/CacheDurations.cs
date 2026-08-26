namespace Toner.Application.Common.Caching;

public static class CacheDurations
{
    // Catálogos que alimentan desplegables. El TTL es solo una red de seguridad: AssetBrands y
    // AssetModels se invalidan ACTIVAMENTE al crear/editar, así que la corrección no depende de él.
    //
    // Para Cities es la única protección que existe, porque NO hay endpoint de escritura de ciudades
    // — las 1.104 filas las siembra DataSeeder desde colombia-cities.json y nada las modifica en
    // runtime. Es decir, lo único de lo que este TTL protege es de una edición manual en la base.
    public static readonly TimeSpan Catalog = TimeSpan.FromHours(4);

    // El dashboard NO tiene invalidación activa: recalcularlo ante cada ticket, orden o time log que
    // cambie sería mucha complejidad para el beneficio. 30 segundos de antigüedad son aceptables en
    // un resumen de indicadores; no lo serían en una operación transaccional.
    public static readonly TimeSpan DashboardSummary = TimeSpan.FromSeconds(30);
}
