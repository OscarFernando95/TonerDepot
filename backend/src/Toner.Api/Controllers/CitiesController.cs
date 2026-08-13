using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Cities;
using Toner.Application.Cities.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/cities")]
[Authorize(Roles = RoleNames.StaffRoles)]
public class CitiesController : ControllerBase
{
    private readonly ICityService _cityService;
    private readonly IValidator<CreateCityRequest> _createValidator;

    public CitiesController(ICityService cityService, IValidator<CreateCityRequest> createValidator)
    {
        _cityService = cityService;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CityDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _cityService.ListAsync(cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Administrador)]
    public async Task<ActionResult<CityDto>> Create([FromBody] CreateCityRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var city = await _cityService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { }, city);
    }
}
