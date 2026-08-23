using MediatR;

namespace WellSense.Application.Sync.SyncMeasurements;

public record SyncMeasurementsCommand(
    Guid CurrentUserId,
    Guid DeviceId,
    string RequestId,
    IReadOnlyList<MeasurementItem> Measurements) : IRequest<SyncMeasurementsResult>;

/// <summary>Item de entrada. `Id` es el eventId generado por el cliente (watch/phone) — la clave de idempotencia por medición individual.</summary>
public record MeasurementItem(Guid Id, string Type, decimal Value, string Unit, DateTimeOffset RecordedAt);

public record RejectedItem(Guid Id, string Reason);

public record SyncMeasurementsResult(
    string RequestId,
    string Status,
    int AcceptedCount,
    int DuplicatedCount,
    int RejectedCount,
    IReadOnlyList<RejectedItem> RejectedItems);
