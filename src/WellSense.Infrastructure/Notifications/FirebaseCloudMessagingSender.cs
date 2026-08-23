using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Notifications;

/// <summary>
/// Implementación real contra la API de FCM (no un stub que solo loguea, a diferencia
/// de LoggingEmailSender del Bloque 2) — ver justificación en IPushNotificationSender.
/// Sin `Firebase:CredentialsPath` configurado (el JSON de la cuenta de servicio, que
/// coloca el Chat DevSecOps), el push queda deshabilitado de forma segura: nunca lanza,
/// solo loguea una advertencia una vez y devuelve `false` en cada intento — la app entera
/// no debe fallar por esto, ni al arrancar ni en cada request.
/// </summary>
public class FirebaseCloudMessagingSender(
    IConfiguration configuration, ILogger<FirebaseCloudMessagingSender> logger) : IPushNotificationSender
{
    private static readonly object InitLock = new();
    private static FirebaseApp? _app;
    private static bool _warnedOnce;

    public async Task<bool> TrySendAsync(string fcmToken, string title, string body, CancellationToken ct = default)
    {
        var app = GetOrCreateApp();
        if (app is null) return false;

        try
        {
            var message = new Message
            {
                Token = fcmToken,
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body }
            };
            await FirebaseMessaging.GetMessaging(app).SendAsync(message, ct);
            return true;
        }
        catch (Exception ex)
        {
            // Nunca propagar — un push fallido (token inválido/expirado, FCM caído, etc.)
            // nunca debe tumbar el flujo que lo llama. Ver IPushNotificationSender.
            logger.LogWarning(ex, "Push a FCM falló para un token registrado.");
            return false;
        }
    }

    private FirebaseApp? GetOrCreateApp()
    {
        if (_app is not null) return _app;
        lock (InitLock)
        {
            if (_app is not null) return _app;

            var credentialsPath = configuration["Firebase:CredentialsPath"];
            if (string.IsNullOrWhiteSpace(credentialsPath) || !File.Exists(credentialsPath))
            {
                if (!_warnedOnce)
                {
                    _warnedOnce = true;
                    logger.LogWarning(
                        "Firebase:CredentialsPath no configurado o el archivo no existe — el push " +
                        "queda deshabilitado (nunca lanza, solo no envía) hasta que DevSecOps coloque " +
                        "las credenciales reales.");
                }
                return null;
            }

            _app = FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(credentialsPath) });
            return _app;
        }
    }
}
