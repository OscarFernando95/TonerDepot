namespace Toner.Application.Users.Dtos;

// Solo lo devuelven CreateAsync y ResetPasswordAsync — nunca ListAsync/UpdateAsync (UserDto normal).
// Es la ÚNICA vía por la que la contraseña en claro sale de UserService: para mostrarla una vez en
// pantalla al Administrador que crea/resetea (hallazgo #3 de SECURITY_AUDIT.md). No se persiste en
// ningún lado con este valor legible; el envío por correo va cifrado (ver IBackgroundJobScheduler).
public class UserWithGeneratedPasswordDto : UserDto
{
    public string GeneratedPassword { get; set; } = string.Empty;
}
