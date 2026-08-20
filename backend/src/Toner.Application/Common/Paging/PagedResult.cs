namespace Toner.Application.Common.Paging;

// Envelope único para todos los listados paginados (CODE_QUALITY_AUDIT.md hallazgo #4).
//
// Items, PageSize y HasMore son el núcleo común a los dos modos de paginación. Los otros tres son
// nullable a propósito, porque cada modo solo puede llenar los suyos:
//
//   - Offset  -> TotalCount y Page. NextCursor queda null.
//   - Cursor  -> NextCursor. TotalCount y Page quedan null: en keyset no existe "número de página",
//                y calcular el total exigiría un COUNT(*) que anula el beneficio de no usar OFFSET.
//
// Tener NextCursor declarado desde el principio permite migrar un endpoint de offset a cursor sin
// romper el contrato.
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    // Tamaño de página EFECTIVO (ya recortado a MaxPageSize), no el que pidió el cliente.
    public required int PageSize { get; init; }

    public required bool HasMore { get; init; }

    public int? TotalCount { get; init; }
    public int? Page { get; init; }
    public string? NextCursor { get; init; }
}
