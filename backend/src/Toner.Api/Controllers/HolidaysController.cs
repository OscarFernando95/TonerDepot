using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Calendar;
using Toner.Application.Calendar.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

// Festivos de Colombia (calculados) más los ajustes de la empresa. Lectura para todo el staff;
// los ajustes solo los hace el Administrador.
[ApiController]
[Route("api/holidays")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class HolidaysController : ControllerBase
{
    private readonly IHolidayService _service;
    private readonly IValidator<SetHolidayOverrideRequest> _validator;

    public HolidaysController(IHolidayService service, IValidator<SetHolidayOverrideRequest> validator)
    {
        _service = service;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> List([FromQuery] int? year, CancellationToken cancellationToken)
    {
        var y = year ?? DateTime.UtcNow.Year;
        if (y is < 2000 or > 2100)
        {
            return BadRequest(new { title = "El año debe estar entre 2000 y 2100." });
        }

        return Ok(await _service.ListAsync(y, cancellationToken));
    }

    [HttpPut("overrides")]
    [Authorize(Roles = RoleNames.Administrador)]
    public async Task<ActionResult<HolidayDto>> SetOverride([FromBody] SetHolidayOverrideRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        return Ok(await _service.SetOverrideAsync(request, cancellationToken));
    }

    [HttpDelete("overrides/{date}")]
    [Authorize(Roles = RoleNames.Administrador)]
    public async Task<IActionResult> RemoveOverride(string date, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return BadRequest(new { title = "La fecha debe tener el formato yyyy-MM-dd." });
        }

        await _service.RemoveOverrideAsync(parsed, cancellationToken);
        return NoContent();
    }
}
