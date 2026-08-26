namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Envío de correo (verificación de email, reset de password). Implementación real:
/// SmtpEmailSender (MailKit, Bloque post-10) — LoggingEmailSender queda como fallback
/// para ambientes sin SMTP configurado (mismo patrón que FirebaseCloudMessagingSender/
/// StripePaymentGateway: nunca lanza, nunca rompe el arranque, solo no envía).
///
/// `recipientName` es el nombre a mostrar en el saludo ("Hola, {nombre}.") — quien
/// llama decide qué mandar (el propio nombre de Profile si existe, o null); la
/// implementación de IEmailSender decide el fallback final a `toEmail` si viene null o
/// vacío, para no duplicar esa decisión en cada llamador.
/// </summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default);
}
