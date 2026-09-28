using Toner.Application.Auth.Dtos;
using Toner.Application.Users.Dtos;
using Toner.Application.Common.Paging;

namespace Toner.Application.Users;

public interface IUserService
{
    // Genera una contraseña nueva (SecurePasswordGenerator) y la devuelve en claro UNA vez, para que
    // el Administrador la vea en pantalla — ver UserWithGeneratedPasswordDto.
    Task<UserWithGeneratedPasswordDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<UserDto>> ListAsync(int? page, int? pageSize, CancellationToken cancellationToken = default);
    Task<UserDto> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);

    // Actualiza los datos de perfil/contacto (ver comentario en UpdateUserRequest sobre qué queda
    // fuera). Lanza ConflictException si la cédula ya la tiene otro usuario.
    Task<UserDto> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);

    // Restablece la contraseña a una nueva generada (SecurePasswordGenerator) y vuelve a exigir
    // cambio en el próximo login. Solo Administrador (ver UsersController).
    Task<UserWithGeneratedPasswordDto> ResetPasswordAsync(Guid userId, Guid? performedByUserId = null, CancellationToken cancellationToken = default);

    // Cierra todas las sesiones vivas del usuario (p. ej. un técnico con el celular perdido, que por la
    // política de sesión única no podría entrar desde otro dispositivo). Devuelve cuántas cerró.
    Task<int> RevokeSessionsAsync(Guid userId, Guid? performedByUserId = null, CancellationToken cancellationToken = default);

    // Últimas sesiones del usuario, para auditoría.
    Task<IReadOnlyList<UserSessionDto>> ListSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
