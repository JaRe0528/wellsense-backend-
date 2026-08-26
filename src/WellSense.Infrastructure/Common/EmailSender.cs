using Microsoft.Extensions.Logging;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Common;

/// <summary>
/// Fallback para ambientes sin SMTP configurado (`Smtp:Host` vacío) — solo loguea que
/// "se envió" y el link/token en el nivel Information (NUNCA a nivel que se replique a
/// un sistema de terceros sin control). SmtpEmailSender delega aquí automáticamente
/// cuando no hay credenciales, mismo patrón que FirebaseCloudMessagingSender.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default)
    {
        logger.LogInformation("[STUB EMAIL] Verificación de correo para {Email} — token: {Token}", toEmail, rawToken);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default)
    {
        logger.LogInformation("[STUB EMAIL] Reset de contraseña para {Email} — token: {Token}", toEmail, rawToken);
        return Task.CompletedTask;
    }
}
