using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Users;
using Toner.Application.Users.Dtos;
using Toner.Domain.Common;

namespace Toner.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleNames.Administrador)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;

    public UsersController(
        IUserService userService,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator)
    {
        _userService = userService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await _userService.ListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { }, user);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userService.UpdateAsync(id, request, cancellationToken);
        return Ok(user);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<UserDto>> SetStatus(Guid id, [FromBody] SetUserStatusRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.SetActiveStatusAsync(id, request.IsActive, cancellationToken);
        return Ok(user);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<UserDto>> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.ResetPasswordAsync(id, cancellationToken);
        return Ok(user);
    }
}
