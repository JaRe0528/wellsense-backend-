namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Envío de correo (verificación de email, reset de password). En este bloque se
/// implementa como un stub que solo loguea (Serilog) — el envío real por SMTP es
/// responsabilidad de una integración que no está en el alcance del Chat Backend
/// per se (ver 01-ARQUITECTURA-Y-STACK.md: SMTP vive en Infrastructure, pero las
/// credenciales/proveedor real los define el Chat DevSecOps).
/// </summary>
public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string rawToken, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken ct = default);
}
