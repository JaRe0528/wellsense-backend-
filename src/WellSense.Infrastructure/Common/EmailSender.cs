using Microsoft.Extensions.Logging;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Common;

/// <summary>
/// Stub de envío de correo para este bloque: solo loguea que "se envió" y el link/token
/// en el nivel Information (NUNCA a nivel que se replique a un sistema de terceros sin
/// control). La integración SMTP real (proveedor, plantillas, remitente) queda fuera del
/// alcance del Chat Backend según 01-ARQUITECTURA-Y-STACK.md — se reemplaza esta clase
/// por una implementación real cuando el Chat DevSecOps provea las credenciales.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendEmailVerificationAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        logger.LogInformation("[STUB EMAIL] Verificación de correo para {Email} — token: {Token}", toEmail, rawToken);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        logger.LogInformation("[STUB EMAIL] Reset de contraseña para {Email} — token: {Token}", toEmail, rawToken);
        return Task.CompletedTask;
    }
}
