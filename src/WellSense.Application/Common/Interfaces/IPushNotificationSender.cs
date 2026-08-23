namespace WellSense.Application.Common.Interfaces;

/// <summary>
/// Envío de push vía FCM. Igual que IEmailSender (Bloque 2): la implementación real
/// necesita credenciales (el JSON de la cuenta de servicio de Firebase) que no viven en
/// código — las provee el Chat DevSecOps. A diferencia del email (que quedó un stub que
/// solo loguea), aquí SÍ se implementó el envío real contra la API de FCM
/// (FirebaseAdmin), porque Android necesita algo que funcione en cuanto DevSecOps
/// coloque las credenciales, no un stub permanente — ver HANDOFF de este bloque.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Devuelve true si FCM aceptó el mensaje. Un false (token inválido/expirado, etc.)
    /// NUNCA debe tumbar el flujo que lo llama — el registro en `notifications` (el
    /// centro de notificaciones in-app) es independiente de si el push llegó o no.
    /// </summary>
    Task<bool> TrySendAsync(string fcmToken, string title, string body, CancellationToken ct = default);
}
