using MediatR;

namespace WellSense.Application.Notifications.SendNotification;

/// <summary>
/// Servicio reutilizable, no solo un endpoint: cualquier módulo futuro (ej. ML avisando
/// "tu estrés hoy fue alto", o un recordatorio) puede enviar `SendNotificationCommand`
/// vía MediatR sin pasar por HTTP. En este bloque se expone también un endpoint de
/// prueba (`POST /notifications/test`) para que Web/Android puedan validar el flujo
/// completo de punta a punta sin depender de que otro bloque ya dispare notificaciones.
/// </summary>
public record SendNotificationCommand(Guid UserId, string Type, string Title, string Body) : IRequest<SendNotificationResult>;

public record SendNotificationResult(Guid NotificationId, int PushedCount, int FailedPushCount);
