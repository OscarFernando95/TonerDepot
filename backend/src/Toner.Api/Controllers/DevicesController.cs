using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Push;
using Toner.Application.Push.Dtos;

namespace Toner.Api.Controllers;

// Token del dispositivo para notificaciones push. Cualquier usuario autenticado registra el suyo.
[ApiController]
[Route("api/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly IDeviceTokenService _deviceTokens;
    private readonly IValidator<RegisterDeviceTokenRequest> _validator;

    public DevicesController(IDeviceTokenService deviceTokens, IValidator<RegisterDeviceTokenRequest> validator)
    {
        _deviceTokens = deviceTokens;
        _validator = validator;
    }

    // Lo llama la app tras iniciar sesión y cada vez que FCM rota el token. Un usuario tiene a lo sumo un
    // dispositivo por plataforma: registrar otro de la misma plataforma reemplaza al anterior.
    [HttpPut("push-token")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        await _deviceTokens.RegisterAsync(CurrentUserId, request, cancellationToken);
        return NoContent();
    }

    // El usuario apaga las notificaciones de esa plataforma ("Android" | "iOS").
    [HttpDelete("push-token/{platform}")]
    public async Task<IActionResult> Unregister(string platform, CancellationToken cancellationToken)
    {
        await _deviceTokens.UnregisterAsync(CurrentUserId, platform, cancellationToken);
        return NoContent();
    }

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
