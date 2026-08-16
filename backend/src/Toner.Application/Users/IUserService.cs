using Toner.Application.Users.Dtos;

namespace Toner.Application.Users;

public interface IUserService
{
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<UserDto> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    // Actualiza los datos de perfil/contacto (ver comentario en UpdateUserRequest sobre qué queda
    // fuera). Lanza ConflictException si la cédula ya la tiene otro usuario.
    Task<UserDto> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    // Restablece la contraseña a la genérica (PasswordDefaults.DefaultPassword) y vuelve a exigir
    // cambio en el próximo login. Solo Administrador (ver UsersController).
    Task<UserDto> ResetPasswordAsync(Guid userId, Guid? performedByUserId = null, CancellationToken cancellationToken = default);
}
