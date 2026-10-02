namespace Toner.Application.Push.Dtos;

// Platform: "Android" | "iOS". Llega como texto desde el cliente; el validador lo comprueba antes de parsearlo.
public sealed record RegisterDeviceTokenRequest(string Platform, string Token);
