using System.Collections.Concurrent;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Tests.Integration;

/// <summary>Doble de prueba: captura el token en claro que el flujo real solo loguearía.</summary>
public class CapturingEmailSender : IEmailSender
{
    public ConcurrentDictionary<string, string> VerificationTokens { get; } = new();
    public ConcurrentDictionary<string, string> ResetTokens { get; } = new();

    public Task SendEmailVerificationAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        VerificationTokens[toEmail] = rawToken;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string toEmail, string rawToken, CancellationToken ct = default)
    {
        ResetTokens[toEmail] = rawToken;
        return Task.CompletedTask;
    }
}
