namespace Toner.Api.Middleware;

// Marca un endpoint en el que OpenDbConnectionMiddleware NO debe abrir la conexión de la request por adelantado
// (AUDIT.md H-14): endpoints que pasan mucho tiempo transfiriendo bytes (subir o servir una foto de hasta 12 MB
// por red móvil) retendrían una conexión del pool durante toda la transferencia — con muchos técnicos a la vez, el
// pool (limitado por SKU en Azure Flexible Server) se agota y cae toda la API.
//
// Sin la conexión de request, EF abre y cierra una conexión por comando; cada apertura pasa igual por
// TenantContextInterceptor con el usuario ya autenticado, así que el aislamiento RLS no cambia. El precio es un
// set_config por comando: aceptable aquí (3–4 comandos frente a MB de transferencia), NO para endpoints con muchas
// consultas.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class NoRequestScopedDbConnectionAttribute : Attribute
{
}
