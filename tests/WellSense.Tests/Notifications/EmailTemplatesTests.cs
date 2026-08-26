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
        // HtmlEncode (correctamente) convierte "ñ" en "&#241;" — válido y seguro en
        // cualquier cliente de correo, pero significa que el texto plano en español no
        // aparece literal en el HTML crudo. Se decodifica antes de comparar para poder
        // escribir la aserción en español normal, en vez de escribir la entidad HTML a
        // mano — el bug era de esta prueba (comparaba contra texto sin codificar),
        // nunca de la plantilla en sí.
        var decodedHtml = System.Net.WebUtility.HtmlDecode(html);

        decodedHtml.Should().Contain("Restablece tu contraseña");
        decodedHtml.Should().Contain("Restablecer contraseña");
        decodedHtml.Should().Contain("vence en 1 hora");
        decodedHtml.Should().NotContain("vence en 24 horas");
    }

    [Fact]
    public void A_malicious_name_never_breaks_out_of_the_html_it_is_rendered_into()
    {
        var html = EmailTemplates.BuildEmailVerification("<script>alert(1)</script>", "https://example.com/x");

        html.Should().NotContain("<script>alert(1)</script>");
        html.Should().Contain("&lt;script&gt;");
    }
}
