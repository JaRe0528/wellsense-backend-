using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Infrastructure.Common;

/// <summary>
/// Implementación real vía SMTP (MailKit) — no un stub, mismo criterio que
/// FirebaseCloudMessagingSender/StripePaymentGateway. Sin `Smtp:Host` configurado, cae a
/// `LoggingEmailSender` (nunca lanza, nunca rompe el arranque). Un fallo real de envío
/// (credenciales rechazadas, host inalcanzable, etc.) tampoco se propaga — un correo que
/// no se pudo mandar NUNCA debe convertir un registro o un forgot-password exitoso en un
/// 500 para el usuario; se loguea el error y el flujo sigue (la cuenta ya se creó, el
/// token ya se guardó, el usuario puede pedir que se reenvíe más adelante).
///
/// STARTTLS en el puerto configurado (587 por default) — nunca SSL implícito en 465,
/// que es lo que Gmail y la mayoría de proveedores esperan hoy.
/// </summary>
public class SmtpEmailSender(
    IConfiguration configuration,
    ILogger<SmtpEmailSender> logger,
    LoggingEmailSender fallback) : IEmailSender
{
    public Task SendEmailVerificationAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default)
    {
        var url = BuildLink("verificar-correo", rawToken);
        var html = EmailTemplates.BuildEmailVerification(recipientName ?? toEmail, url);
        return SendAsync(
            toEmail, recipientName, "Verifica tu correo — WellSense", html,
            () => fallback.SendEmailVerificationAsync(toEmail, recipientName, rawToken, ct), ct);
    }

    public Task SendPasswordResetAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default)
    {
        var url = BuildLink("restablecer-contrasena", rawToken);
        var html = EmailTemplates.BuildPasswordReset(recipientName ?? toEmail, url);
        return SendAsync(
            toEmail, recipientName, "Restablece tu contraseña — WellSense", html,
            () => fallback.SendPasswordResetAsync(toEmail, recipientName, rawToken, ct), ct);
    }

    private string BuildLink(string path, string rawToken)
    {
        var baseUrl = (configuration["Frontend:BaseUrl"] ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/{path}?token={Uri.EscapeDataString(rawToken)}";
    }

    private async Task SendAsync(
        string toEmail, string? recipientName, string subject, string htmlBody, Func<Task> fallbackSend, CancellationToken ct)
    {
        var host = configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning(
                "Smtp:Host no configurado — el correo real queda deshabilitado (cae a solo loguear) hasta que DevSecOps coloque las credenciales reales.");
            await fallbackSend();
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                configuration["Smtp:FromName"] ?? "WellSense",
                configuration["Smtp:FromAddress"] ?? configuration["Smtp:Username"]));
            message.To.Add(new MailboxAddress(recipientName ?? toEmail, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            var port = configuration.GetValue("Smtp:Port", 587);

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(configuration["Smtp:Username"], configuration["Smtp:Password"], ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            // Nunca debe tumbar el flujo que lo llama — ver resumen de la clase.
            logger.LogError(ex, "Falló el envío real de correo a {Email}.", toEmail);
        }
    }
}
