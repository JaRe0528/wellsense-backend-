using System.Collections.Concurrent;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Tests.Integration;

/// <summary>Doble de prueba: captura el token en claro que el flujo real solo loguearía, más el nombre de saludo que se le pasó.</summary>
public class CapturingEmailSender : IEmailSender
{
    public ConcurrentDictionary<string, string> VerificationTokens { get; } = new();
    public ConcurrentDictionary<string, string> ResetTokens { get; } = new();
    public ConcurrentDictionary<string, string?> VerificationRecipientNames { get; } = new();
    public ConcurrentDictionary<string, string?> ResetRecipientNames { get; } = new();

    public Task SendEmailVerificationAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default)
    {
        VerificationTokens[toEmail] = rawToken;
        VerificationRecipientNames[toEmail] = recipientName;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string? recipientName, string rawToken, CancellationToken ct = default)
    {
        ResetTokens[toEmail] = rawToken;
        ResetRecipientNames[toEmail] = recipientName;
        return Task.CompletedTask;
    }
}
