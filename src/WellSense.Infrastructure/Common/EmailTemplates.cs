using System.Net;

namespace WellSense.Infrastructure.Common;

/// <summary>
/// Plantillas HTML de marca para los 2 correos transaccionales (verificación, reset de
/// password) — mismo layout, mismos 3 colores, solo cambia el texto. Todo en estilos
/// inline con tablas (no CSS externo, no flexbox/grid) porque la mayoría de los clientes
/// de correo (Gmail, Outlook) no soportan bien CSS moderno o `<style>` externo. Fuente
/// Arial/Helvetica — las tipografías propias del proyecto no cargan en clientes de correo.
///
/// Funciones puras (sin I/O) a propósito — se pueden probar exhaustivamente sin SMTP de
/// por medio, mismo criterio que DailyScoringRules (Bloque 7).
/// </summary>
public static class EmailTemplates
{
    private const string Paper = "#F3F5F1";
    private const string Pine = "#1B4B43";
    private const string Pulse = "#E64B3C";
    private const string FontFamily = "Arial, Helvetica, sans-serif";

    public static string BuildEmailVerification(string greetingName, string verificationUrl)
        => BuildActionEmail(
            eyebrow: "Confirma tu cuenta de WellSense",
            title: "Verifica tu correo",
            greetingName: greetingName,
            bodyText: "Confirma tu correo para proteger tu cuenta y completar tu registro.",
            buttonText: "Verificar mi correo",
            buttonUrl: verificationUrl,
            expiryText: "Este enlace es de un solo uso y vence en 24 horas.");

    public static string BuildPasswordReset(string greetingName, string resetUrl)
        => BuildActionEmail(
            eyebrow: "Restablece tu contraseña",
            title: "Restablece tu contraseña",
            greetingName: greetingName,
            bodyText: "Recibimos una solicitud para restablecer tu contraseña. Si no fuiste tú, puedes ignorar este correo con confianza.",
            buttonText: "Restablecer contraseña",
            buttonUrl: resetUrl,
            expiryText: "Este enlace es de un solo uso y vence en 1 hora.");

    private static string BuildActionEmail(
        string eyebrow, string title, string greetingName, string bodyText, string buttonText, string buttonUrl, string expiryText)
    {
        // Todo el texto que puede contener algo ingresado por el usuario (el nombre de
        // saludo, que viene de Profile.FirstName/LastName) se escapa — nunca se
        // interpola HTML crudo de un valor que un usuario controla.
        var safeGreeting = WebUtility.HtmlEncode(greetingName);
        var safeBodyText = WebUtility.HtmlEncode(bodyText);
        var safeButtonUrl = WebUtility.HtmlEncode(buttonUrl);
        var safeButtonText = WebUtility.HtmlEncode(buttonText);
        var safeEyebrow = WebUtility.HtmlEncode(eyebrow);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeExpiryText = WebUtility.HtmlEncode(expiryText);

        return $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>{safeTitle}</title>
        </head>
        <body style="margin:0;padding:0;background-color:{Paper};font-family:{FontFamily};">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:{Paper};padding:32px 16px;">
            <tr>
              <td align="center">
                <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background-color:#FFFFFF;border-radius:8px;overflow:hidden;">
                  <tr>
                    <td style="background-color:{Pine};padding:32px 40px;text-align:center;">
                      <div style="color:#FFFFFF;font-size:28px;font-weight:bold;font-family:{FontFamily};">WellSense</div>
                      <div style="color:#FFFFFF;font-size:11px;font-weight:normal;letter-spacing:2px;text-transform:uppercase;margin-top:6px;font-family:{FontFamily};">Tu cuerpo habla en señales</div>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:40px;">
                      <div style="color:{Pulse};font-size:12px;font-weight:bold;letter-spacing:1px;text-transform:uppercase;font-family:{FontFamily};">{safeEyebrow}</div>
                      <div style="color:#1A1A1A;font-size:24px;font-weight:bold;margin-top:8px;font-family:{FontFamily};">{safeTitle}</div>
                      <p style="color:#333333;font-size:15px;line-height:1.6;margin-top:16px;font-family:{FontFamily};">Hola, {safeGreeting}. {safeBodyText}</p>
                      <table role="presentation" cellpadding="0" cellspacing="0" style="margin-top:24px;">
                        <tr>
                          <td style="background-color:{Pine};border-radius:6px;">
                            <a href="{safeButtonUrl}" style="display:inline-block;padding:14px 32px;color:#FFFFFF;font-size:15px;font-weight:bold;text-decoration:none;font-family:{FontFamily};">{safeButtonText}</a>
                          </td>
                        </tr>
                      </table>
                      <p style="color:#888888;font-size:13px;margin-top:24px;margin-bottom:4px;font-family:{FontFamily};">Si el botón no funciona, copia este enlace:</p>
                      <p style="color:{Pine};font-size:13px;word-break:break-all;margin-top:0;font-family:{FontFamily};">{safeButtonUrl}</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="background-color:#F7F7F5;padding:16px 40px;">
                      <p style="color:#888888;font-size:12px;margin:0;font-family:{FontFamily};">{safeExpiryText}</p>
                    </td>
                  </tr>
                </table>
                <p style="color:#999999;font-size:12px;text-align:center;margin-top:24px;font-family:{FontFamily};">© 2026 WellSense · Mensaje automático</p>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }
}
