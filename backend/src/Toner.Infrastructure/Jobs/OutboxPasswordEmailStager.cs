using Toner.Application.Common.Interfaces;
using Toner.Domain.Entities;
using Toner.Infrastructure.Persistence;
using Toner.Infrastructure.Security;

namespace Toner.Infrastructure.Jobs;

// IBackgroundJobScheduler sobre el outbox (AUDIT.md H-19): cifra la contraseña y AÑADE la fila al mismo
// TonerDbContext scoped que usa UserService, sin llamar a SaveChanges — la persiste el SaveChanges del caller,
// en la misma transacción que el usuario (patrón de colaborador del proyecto, CLAUDE.md / hallazgo #8).
public class OutboxPasswordEmailStager : IBackgroundJobScheduler
{
    private readonly TonerDbContext _db;
    private readonly IJobPayloadEncryptor _encryptor;

    public OutboxPasswordEmailStager(TonerDbContext db, IJobPayloadEncryptor encryptor)
    {
        _db = db;
        _encryptor = encryptor;
    }

    public void StageGeneratedPasswordEmail(string cedula, string generatedPassword)
    {
        // Cifrada ACÁ, antes de tocar cualquier almacenamiento durable (hallazgo #3).
        _db.PasswordEmailOutbox.Add(new PasswordEmailOutbox
        {
            Cedula = cedula,
            EncryptedPassword = _encryptor.Encrypt(generatedPassword)
        });
    }
}
