namespace Toner.Application.Assets.Dtos;

// Filtros del listado de activos, en el SERVIDOR y antes de paginar (AUDIT.md H-11, Paso 3). Opcionales: un cliente
// que no los manda recibe lo de siempre. Son un AND con el alcance del rol (un Cliente solo ve activos de sus sedes
// aunque pase otro clientId).
//
// ClientLocationId y Status existen sobre todo para los selectores (activos de una sede, activos instalados, activos
// en bodega), que antes pedían TODO el inventario y se truncaban en silencio a 200.
public sealed record AssetListFilter(
    string? Search = null, Guid? CityId = null, Guid? ClientId = null, Guid? ClientLocationId = null, string? Status = null);
