namespace Toner.Application.Common.Paging;

public static class PagingDefaults
{
    // Tope duro. Un pageSize mayor se RECORTA en silencio, no se obedece ni se rechaza: el cliente
    // recibe MaxPageSize filas y el campo PageSize del envelope le dice el tamaño real que se aplicó.
    public const int MaxPageSize = 200;

    // Default de la FASE DE COMPATIBILIDAD (Pasos 1+2 del plan B.5): deliberadamente igual al máximo.
    // Mientras las vistas no paginen, la capa api/*.ts desenvuelve .items y devuelve el array plano,
    // así que este default no es una decisión de UX sino de compatibilidad — es "cuántas filas ve una
    // vista que todavía no sabe que existe la paginación". Bajarlo ahora solo truncaría antes las
    // tablas existentes sin que nadie pueda pedir la página siguiente.
    public const int DefaultPageSize = MaxPageSize;

    // Valor de destino para cuando el Paso 3 le dé UI de paginación a cada vista y el default pase a
    // significar "primera página". Queda declarado para que ese cambio sea de una línea.
    public const int PagedUiDefaultPageSize = 50;

    public static int ResolvePageSize(int? requested) =>
        Math.Clamp(requested ?? DefaultPageSize, 1, MaxPageSize);

    public static int ResolvePage(int? requested) =>
        Math.Max(requested ?? 1, 1);
}
