export interface PositionFix {
  latitude: number
  longitude: number
  accuracyMeters: number
}

// Ubicación actual del navegador. NUNCA lanza: si el usuario niega el permiso, no hay GPS o tarda demasiado,
// devuelve null — el servidor lo registra como "sin ubicación" y alerta, pero el check-in no se bloquea.
export function getCurrentPosition(timeoutMs = 10000): Promise<PositionFix | null> {
  if (!('geolocation' in navigator)) {
    return Promise.resolve(null)
  }

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyMeters: position.coords.accuracy
        }),
      (error) => {
        console.warn('No se pudo obtener la ubicación:', error.message)
        resolve(null)
      },
      { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 30000 }
    )
  })
}
