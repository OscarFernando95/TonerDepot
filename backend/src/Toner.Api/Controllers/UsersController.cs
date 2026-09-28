using System.Security.Claims;
using Toner.Application.Common.Paging;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toner.Application.Auth.Dtos;
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
    public async Task<ActionResult<PagedResult<UserDto>>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        return Ok(await _userService.ListAsync(page, pageSize, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UserWithGeneratedPasswordDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
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
    public async Task<ActionResult<UserWithGeneratedPasswordDto>> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userService.ResetPasswordAsync(id, CurrentUserId, cancellationToken);
        return Ok(user);
    }

    [HttpGet("{id:guid}/sessions")]
    public async Task<ActionResult<IReadOnlyList<UserSessionDto>>> ListSessions(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _userService.ListSessionsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/sessions/revoke")]
    public async Task<ActionResult<object>> RevokeSessions(Guid id, CancellationToken cancellationToken)
    {
        var revoked = await _userService.RevokeSessionsAsync(id, CurrentUserId, cancellationToken);
        return Ok(new { revoked });
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
