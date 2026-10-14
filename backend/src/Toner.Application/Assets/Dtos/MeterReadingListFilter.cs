namespace Toner.Application.Assets.Dtos;

// Filtros del listado de lecturas de contador, en el SERVIDOR y antes de paginar (AUDIT.md H-11, Paso 3). Opcionales:
// un cliente que no los manda (la app móvil) recibe lo de siempre. Son un AND con el alcance del rol.
public sealed record MeterReadingListFilter(string? Search = null, Guid? CityId = null, Guid? ClientId = null);
