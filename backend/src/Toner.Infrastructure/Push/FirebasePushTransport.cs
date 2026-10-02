using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Toner.Application.Push;

namespace Toner.Infrastructure.Push;

public sealed class FirebasePushTransport : IPushTransport
{
    // Canal de Android en el que la app muestra estas notificaciones (se crea en la app con este mismo id).
    public const string AndroidChannelId = "toner_assignments";

    private readonly FirebaseMessaging? _messaging;
    private readonly ILogger<FirebasePushTransport> _logger;

    public FirebasePushTransport(IOptions<FirebaseSettings> options, ILogger<FirebasePushTransport> logger)
    {
        _logger = logger;

        var path = options.Value.CredentialsPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("Firebase:CredentialsPath no está configurado: las notificaciones push están apagadas.");
            return;
        }

        if (!File.Exists(path))
        {
            _logger.LogWarning("El archivo de credenciales de Firebase no existe en la ruta configurada: las notificaciones push están apagadas.");
            return;
        }

        try
        {
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = CredentialFactory.FromFile<ServiceAccountCredential>(path).ToGoogleCredential()
            });
            _messaging = FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo inicializar Firebase con las credenciales configuradas: las notificaciones push están apagadas.");
        }
    }

    public bool IsEnabled => _messaging is not null;

    public async Task<IReadOnlyList<PushOutcome>> SendAsync(
        IReadOnlyList<PushTarget> targets, PushMessage message, CancellationToken cancellationToken = default)
    {
        if (_messaging is null || targets.Count == 0)
        {
            return targets.Select(_ => PushOutcome.Failed).ToList();
        }

        // Message.Token figura como obsoleto en favor de Fid (ID de instalación de Firebase), pero el token de registro
        // de FCM que entrega firebase_messaging sigue siendo el que va en Token.
#pragma warning disable CS0618
        var messages = targets.Select(t => new Message
        {
            Token = t.Token,
            Notification = new Notification { Title = message.Title, Body = message.Body },
            Data = message.Data.ToDictionary(kv => kv.Key, kv => kv.Value),
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification { ChannelId = AndroidChannelId }
            },
            Apns = new ApnsConfig { Aps = new Aps { Sound = "default" } }
        }).ToList();
#pragma warning restore CS0618

        var response = await _messaging.SendEachAsync(messages, cancellationToken);

        return response.Responses.Select(r =>
        {
            if (r.IsSuccess)
            {
                return PushOutcome.Sent;
            }

            // Solo "no registrado" invalida el token; un argumento inválido o un fallo transitorio no deben borrarlo.
            return r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered
                ? PushOutcome.InvalidToken
                : PushOutcome.Failed;
        }).ToList();
    }
}
