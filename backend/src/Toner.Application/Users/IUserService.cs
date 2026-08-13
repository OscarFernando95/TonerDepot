using Toner.Application.Users.Dtos;

namespace Toner.Application.Users;

public interface IUserService
{
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<UserDto> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    // Restablece la contraseña a la genérica (PasswordDefaults.DefaultPassword) y vuelve a exigir
    // cambio en el próximo login. Solo Administrador (ver UsersController).
    Task<UserDto> ResetPasswordAsync(Guid userId, CancellationToken cancellationToken = default);
}
