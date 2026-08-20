using Microsoft.EntityFrameworkCore;

namespace Toner.Application.Common.Paging;

public static class QueryablePagingExtensions
{
    // Paginación por offset: para catálogos acotados y listados operacionales donde el usuario salta
    // a una página concreta y quiere ver el total. El COUNT extra se paga a propósito — es lo que
    // permite llenar TotalCount, y sobre conjuntos de este tamaño es barato.
    public static async Task<PagedResult<T>> ToOffsetPageAsync<T>(
        this IQueryable<T> query,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        var size = PagingDefaults.ResolvePageSize(pageSize);
        var pageNumber = PagingDefaults.ResolvePage(page);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            PageSize = size,
            Page = pageNumber,
            TotalCount = total,
            HasMore = (long)pageNumber * size < total
        };
    }

    // Paginación por cursor (keyset): para series temporales append-only, donde alguien sí pagina
    // profundo y OFFSET degradaría (generar y descartar N filas). Coste constante a cualquier
    // profundidad, y estable ante inserciones concurrentes.
    //
    // El caller entrega la consulta YA filtrada por el cursor y YA ordenada — a propósito. La
    // alternativa (que el helper construyera el WHERE del keyset genéricamente) exige componer
    // árboles de expresión y vuelve ilegible tanto el helper como el error cuando algo no traduce.
    // Acá cada sitio escribe su predicado en cuatro líneas evidentes.
    //
    // Pide una fila de más (size + 1) para saber si hay página siguiente sin un COUNT aparte.
    public static async Task<PagedResult<T>> ToCursorPageAsync<T>(
        this IQueryable<T> orderedQuery,
        int? pageSize,
        Func<T, string> nextCursorFrom,
        CancellationToken cancellationToken = default)
    {
        var size = PagingDefaults.ResolvePageSize(pageSize);

        var items = await orderedQuery.Take(size + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > size;
        if (hasMore)
        {
            items.RemoveAt(size);
        }

        return new PagedResult<T>
        {
            Items = items,
            PageSize = size,
            HasMore = hasMore,
            NextCursor = hasMore ? nextCursorFrom(items[^1]) : null
        };
    }
}
