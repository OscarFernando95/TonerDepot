using Toner.Domain.Enums;

namespace Toner.Application.Geo;

public class GeoOptions
{
    // Distancia máxima sede-técnico para considerar "en sitio". Holgada a propósito: un GPS en interiores
    // se equivoca fácil por decenas o cientos de metros, y como solo se registra y se alerta (no se
    // bloquea), un radio estrecho solo generaría falsas alarmas.
    public double SiteRadiusMeters { get; set; } = 250;
}

public static class GeoDistance
{
    private const double EarthRadiusMeters = 6_371_000;

    // Fórmula de Haversine: distancia sobre la esfera entre dos puntos WGS84.
    public static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double deg) => deg * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static bool IsValidCoordinate(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
}

public readonly record struct LocationEvaluation(LocationStatus Status, double? DistanceMeters);

public static class LocationEvaluator
{
    // Compara la ubicación reportada con la de la sede. Nunca lanza: una ubicación ausente o inválida es
    // un estado (SinUbicacion), no un error, porque el check-in no se bloquea por eso.
    public static LocationEvaluation Evaluate(
        double? siteLatitude, double? siteLongitude, double? latitude, double? longitude, double radiusMeters)
    {
        if (latitude is null || longitude is null || !GeoDistance.IsValidCoordinate(latitude.Value, longitude.Value))
        {
            return new LocationEvaluation(LocationStatus.SinUbicacion, null);
        }

        if (siteLatitude is null || siteLongitude is null)
        {
            return new LocationEvaluation(LocationStatus.SedeSinCoordenadas, null);
        }

        var distance = GeoDistance.Meters(siteLatitude.Value, siteLongitude.Value, latitude.Value, longitude.Value);
        return new LocationEvaluation(distance <= radiusMeters ? LocationStatus.EnSitio : LocationStatus.FueraDeSitio, Math.Round(distance, 1));
    }
}
