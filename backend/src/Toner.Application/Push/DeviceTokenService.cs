using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Toner.Application.Common.Interfaces;
using Toner.Application.Push.Dtos;
using Toner.Domain.Entities;
using Toner.Domain.Enums;

namespace Toner.Application.Push;

public class DeviceTokenService : IDeviceTokenService
{
    private readonly IApplicationDbContext _db;

    public DeviceTokenService(IApplicationDbContext db) => _db = db;

    public async Task RegisterAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default)
    {
        var platform = ParsePlatform(request.Platform);
        var token = request.Token.Trim();

        // Un dispositivo físico es de un solo usuario: si el token estaba a nombre de otro (alguien más inició sesión
        // en este mismo teléfono) o de este mismo usuario en otra plataforma, esa fila se retira.
        var stale = await _db.DeviceTokens
            .Where(d => d.Token == token && (d.UserId != userId || d.Platform != platform))
            .ToListAsync(cancellationToken);
        _db.DeviceTokens.RemoveRange(stale);

        var current = await _db.DeviceTokens
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Platform == platform, cancellationToken);

        if (current is null)
        {
            _db.DeviceTokens.Add(new DeviceToken { UserId = userId, Platform = platform, Token = token });
        }
        else if (current.Token != token)
        {
            // Otro dispositivo de la misma plataforma: reemplaza al anterior.
            current.Token = token;
            current.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterAsync(Guid userId, string platform, CancellationToken cancellationToken = default)
    {
        var parsed = ParsePlatform(platform);
        var rows = await _db.DeviceTokens.Where(d => d.UserId == userId && d.Platform == parsed).ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        _db.DeviceTokens.RemoveRange(rows);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static PushPlatform ParsePlatform(string value) =>
        Enum.TryParse<PushPlatform>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new ValidationException("La plataforma debe ser Android o iOS.");
}
