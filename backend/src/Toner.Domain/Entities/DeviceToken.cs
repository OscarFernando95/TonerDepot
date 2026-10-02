using Toner.Domain.Common;
using Toner.Domain.Enums;

namespace Toner.Domain.Entities;

// Token FCM del dispositivo al que se le envían las notificaciones push de un usuario. Como máximo UNO por
// usuario y plataforma (un Android y un iPhone): registrar otro dispositivo de la misma plataforma reemplaza el
// token. Está atado al usuario y NO a la sesión, a propósito: la notificación debe llegar aunque la sesión esté
// cerrada (decisión del producto), así que cerrar sesión no lo borra.
//
// Sin RLS por cliente, igual que UserSessions: es metadato del dispositivo (usuario + token opaco), sin datos de
// clientes, y lo lee quien emite el aviso (otro usuario o un job) para llegar al técnico destinatario.
public class DeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public PushPlatform Platform { get; set; }

    // Token opaco de FCM. Es único en toda la tabla: un dispositivo físico pertenece a un solo usuario a la vez.
    public string Token { get; set; } = string.Empty;
}
