namespace Toner.Application.Common.Paging;

public static class PagedResultFactory
{
    // Para los cortocircuitos que devuelven vacío sin llegar a consultar (ej. un técnico sin ninguna
    // ciudad de cobertura). Mantiene el envelope coherente: el cliente recibe la misma forma que en
    // cualquier otra respuesta, con Items vacío y HasMore en false.
    public static PagedResult<T> Empty<T>(int? pageSize) => new()
    {
        Items = Array.Empty<T>(),
        PageSize = PagingDefaults.ResolvePageSize(pageSize),
        HasMore = false,
        Page = 1,
        TotalCount = 0
    };

    public static PagedResult<T> EmptyCursor<T>(int? pageSize) => new()
    {
        Items = Array.Empty<T>(),
        PageSize = PagingDefaults.ResolvePageSize(pageSize),
        HasMore = false,
        NextCursor = null
    };
}
