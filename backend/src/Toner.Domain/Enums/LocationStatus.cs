namespace Toner.Domain.Enums;

// Resultado de comparar la ubicación del técnico al hacer check-in/check-out con la de la sede. Solo se
// registra y se alerta (decisión de producto): nunca bloquea el check-in.
public enum LocationStatus
{
    EnSitio = 0,
    FueraDeSitio = 1,
    // El técnico no envió ubicación (permiso denegado, GPS apagado).
    SinUbicacion = 2,
    // La sede aún no tiene coordenadas, no se puede comparar.
    SedeSinCoordenadas = 3
}
