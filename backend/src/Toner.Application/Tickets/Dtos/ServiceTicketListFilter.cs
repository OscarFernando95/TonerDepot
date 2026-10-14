namespace Toner.Application.Tickets.Dtos;

// Filtros del listado de tickets que se aplican EN EL SERVIDOR, antes de paginar (AUDIT.md H-11, Paso 3). Todos
// opcionales: un cliente viejo que no los manda recibe el listado de siempre. Son un AND con el aislamiento del rol
// (un Cliente o un Técnico nunca ve más por pasar otro clientId).
public sealed record ServiceTicketListFilter(
    string? Status = null,
    string? Priority = null,
    Guid? ClientId = null,
    string? Search = null);
