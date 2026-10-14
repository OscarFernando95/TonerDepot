using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Toner.Api.RateLimiting;

// IP real del cliente detrás de un reverse proxy (SECURITY_AUDIT.md #16; App Service: AUDIT.md H-13). Sin esto,
// RemoteIpAddress sería la del proxy para todos y el rate limit por IP compartiría un único cupo.
//
// Configuración "ReverseProxy":
//   - KnownProxies / KnownNetworks: proxies concretos en los que confiar (por defecto ninguno: ASP.NET Core solo
//     confía en loopback, así que sin proxy el X-Forwarded-For se ignora y no se puede falsificar la IP).
//   - TrustAllProxies (bool, false): confía en el salto INMEDIATO sea cual sea su IP. Es lo que necesita Azure App
//     Service, cuyo front no tiene IPs fijas y es el ÚNICO camino hasta el worker. Activarlo SOLO ahí: si el puerto
//     de la app quedara expuesto, cualquiera podría falsificar su IP.
//   - ForwardLimit (int, 1): cuántos saltos de X-Forwarded-For se aceptan, empezando por el último. Con 1, un
//     "X-Forwarded-For: <falsa>" del cliente queda a la izquierda de la IP que añade el proxy y se ignora. Subir a 2
//     si se pone Front Door o Application Gateway delante de App Service.
public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = Math.Max(1, configuration.GetValue("ReverseProxy:ForwardLimit", 1));

        if (configuration.GetValue("ReverseProxy:TrustAllProxies", false))
        {
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
            return;
        }

        foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
        {
            if (IPAddress.TryParse(proxy, out var address))
            {
                options.KnownProxies.Add(address);
            }
        }

        foreach (var network in configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
        {
            var parts = network.Split('/');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var prefixLength))
            {
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
            }
        }
    }
}
