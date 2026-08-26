using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WellSense.Infrastructure.Common;
using Xunit;

namespace WellSense.Tests.Notifications;

public class SmtpEmailSenderTests
{
    private static IConfiguration ConfigWithout(Dictionary<string, string?>? overrides = null)
        => new ConfigurationBuilder().AddInMemoryCollection(overrides ?? []).Build();

    [Fact]
    public async Task Falls_back_to_logging_when_smtp_host_is_not_configured_and_never_throws()
    {
        // Sin Smtp:Host — no hay forma de conectar de verdad a nada en esta prueba, y
        // no debe intentarlo: debe caer a LoggingEmailSender en silencio.
        var config = ConfigWithout();
        var fallback = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
        var sender = new SmtpEmailSender(config, NullLogger<SmtpEmailSender>.Instance, fallback);

        var act = () => sender.SendEmailVerificationAsync("user@example.com", "Ana", "tok-123", default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Password_reset_also_falls_back_without_throwing()
    {
        var config = ConfigWithout();
        var fallback = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
        var sender = new SmtpEmailSender(config, NullLogger<SmtpEmailSender>.Instance, fallback);

        var act = () => sender.SendPasswordResetAsync("user@example.com", null, "tok-456", default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task An_unreachable_configured_host_never_propagates_an_exception_to_the_caller()
    {
        // Smtp:Host SÍ está "configurado" pero apunta a algo que no existe — simula un
        // fallo real de red/DNS. El registro/forgot-password NUNCA debe romperse por
        // esto (ver SmtpEmailSender: se loguea, no se propaga).
        var config = ConfigWithout(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.this-host-does-not-exist.invalid",
            ["Smtp:Port"] = "587",
            ["Smtp:Username"] = "user",
            ["Smtp:Password"] = "pass"
        });
        var fallback = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance);
        var sender = new SmtpEmailSender(config, NullLogger<SmtpEmailSender>.Instance, fallback);

        var act = () => sender.SendEmailVerificationAsync("user@example.com", "Ana", "tok-789", default);

        await act.Should().NotThrowAsync();
    }
}
