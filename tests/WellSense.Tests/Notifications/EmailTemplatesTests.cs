using FluentAssertions;
using WellSense.Infrastructure.Common;
using Xunit;

namespace WellSense.Tests.Notifications;

public class EmailTemplatesTests
{
    [Fact]
    public void Verification_email_contains_the_real_link_the_greeting_and_the_24h_expiry_text()
    {
        var html = EmailTemplates.BuildEmailVerification("Ana Torres", "https://wellsense-web.vercel.app/verificar-correo?token=abc123");

        html.Should().Contain("https://wellsense-web.vercel.app/verificar-correo?token=abc123");
        html.Should().Contain("Hola, Ana Torres.");
        html.Should().Contain("Verificar mi correo");
        html.Should().Contain("vence en 24 horas");
        html.Should().Contain("#1B4B43"); // Pine
        html.Should().Contain("#E64B3C"); // Pulse
    }

    [Fact]
    public void Password_reset_email_uses_the_1h_expiry_text_and_its_own_copy()
    {
        var html = EmailTemplates.BuildPasswordReset("Ana Torres", "https://wellsense-web.vercel.app/restablecer-contrasena?token=xyz789");

        html.Should().Contain("Restablece tu contraseña");
        html.Should().Contain("Restablecer contraseña");
        html.Should().Contain("vence en 1 hora");
        html.Should().NotContain("vence en 24 horas");
    }

    [Fact]
    public void A_malicious_name_never_breaks_out_of_the_html_it_is_rendered_into()
    {
        var html = EmailTemplates.BuildEmailVerification("<script>alert(1)</script>", "https://example.com/x");

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;");
    }
}
