using WellSense.Application.Common.Interfaces;

namespace WellSense.Tests.TestHelpers;

public class FixedClock(DateTimeOffset now) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

/// <summary>Hash reversible y rápido — solo para pruebas, nunca usar en producción.</summary>
public class PlainTextPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"HASH::{password}";
    public bool Verify(string password, string hash) => hash == $"HASH::{password}";
}

/// <summary>Genera tokens/códigos deterministas y crecientes para que las pruebas sean reproducibles.</summary>
public class SequentialTokenGenerator : ITokenGenerator
{
    private int _tokenCounter;
    private readonly Queue<string> _codes = new();

    public void EnqueueCode(string code) => _codes.Enqueue(code);

    public string GenerateUrlSafeToken(int byteLength = 32) => $"token-{++_tokenCounter}";

    public string GenerateSixDigitCode() => _codes.Count > 0 ? _codes.Dequeue() : (++_tokenCounter).ToString("D6");

    public string Sha256Hex(string value) => $"sha256::{value}";
}

public class FakeDeviceLinkCodeHasher : IDeviceLinkCodeHasher
{
    public string Hash(string sixDigitCode) => $"hmac::{sixDigitCode}";
}

public class FakeJwtTokenService : IJwtTokenService
{
    public string GenerateAccessToken(Guid userId, string email, string role) => $"jwt-for-{userId}";
}

public class ControllableViolationDetector : IUniqueConstraintViolationDetector
{
    private readonly bool _alwaysReturn;
    public ControllableViolationDetector(bool alwaysReturn = true) => _alwaysReturn = alwaysReturn;
    public bool IsUniqueViolation(Exception ex, string constraintName) => _alwaysReturn;
}

/// <summary>
/// No-op de MediatR.IPublisher para handlers (ej. SyncMeasurementsCommandHandler, desde
/// Bloque 5) que publican un evento de integración pero cuyo resultado no depende de que
/// alguien lo escuche — las pruebas de esos handlers no necesitan verificar el push a
/// SignalR (eso lo cubre MeasurementsSyncedEventHandlerTests por separado), solo que el
/// propio handler no truene al intentar publicar.
/// </summary>
public class NoOpPublisher : MediatR.IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : MediatR.INotification => Task.CompletedTask;
}

public record CapturedDashboardNotification(Guid UserId, string EventType, object Payload);

public class SpyDashboardNotifier : IDashboardNotifier
{
    public List<CapturedDashboardNotification> Calls { get; } = [];

    public Task NotifyUserAsync(Guid userId, string eventType, object payload, CancellationToken ct = default)
    {
        Calls.Add(new CapturedDashboardNotification(userId, eventType, payload));
        return Task.CompletedTask;
    }
}

public class RecordingPushNotificationSender : IPushNotificationSender
{
    public List<(string Token, string Title, string Body)> Sent { get; } = [];
    public bool AlwaysSucceeds { get; set; } = true;

    public Task<bool> TrySendAsync(string fcmToken, string title, string body, CancellationToken ct = default)
    {
        Sent.Add((fcmToken, title, body));
        return Task.FromResult(AlwaysSucceeds);
    }
}

/// <summary>
/// Nunca cobra de verdad — registra cada intento (para verificar que ChargeAsync se
/// llama EXACTAMENTE una vez por Subscribe, nunca dos) y responde con el resultado que
/// la prueba configure. Default: aprueba, tarjeta "visa"/"4242".
/// </summary>
public class FakePaymentGateway : IPaymentGateway
{
    public bool NextApproved { get; set; } = true;
    public string? NextDeclineReason { get; set; } = "insufficient_funds";
    public List<(string Token, int AmountCents, string Currency, string IdempotencyKey)> Charges { get; } = [];

    public Task<ChargeResult> ChargeAsync(
        string paymentMethodToken, int amountCents, string currency, string idempotencyKey, CancellationToken ct = default)
    {
        Charges.Add((paymentMethodToken, amountCents, currency, idempotencyKey));
        return Task.FromResult(NextApproved
            ? new ChargeResult(true, $"tx-{Guid.NewGuid()}", "visa", "4242", null)
            : new ChargeResult(false, $"tx-{Guid.NewGuid()}", null, null, NextDeclineReason));
    }
}

public record CapturedDeviceCommandNotification(Guid DeviceId, object Command);

public class SpyDeviceCommandNotifier : IDeviceCommandNotifier
{
    public List<CapturedDeviceCommandNotification> Calls { get; } = [];
    public bool ThrowOnNotify { get; set; }

    public Task NotifyDeviceAsync(Guid deviceId, object command, CancellationToken ct = default)
    {
        if (ThrowOnNotify) throw new InvalidOperationException("simulated push failure");
        Calls.Add(new CapturedDeviceCommandNotification(deviceId, command));
        return Task.CompletedTask;
    }
}
