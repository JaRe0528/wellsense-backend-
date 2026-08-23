namespace WellSense.Api.Contracts;

public record SyncMeasurementItemRequest(Guid Id, string Type, decimal Value, string Unit, DateTimeOffset RecordedAt);

public record SyncMeasurementsRequest(Guid DeviceId, string RequestId, List<SyncMeasurementItemRequest> Measurements);

public record RejectedItemResponse(Guid Id, string Reason);

public record SyncMeasurementsResponse(
    string RequestId,
    string Status,
    int AcceptedCount,
    int DuplicatedCount,
    int RejectedCount,
    IReadOnlyList<RejectedItemResponse> RejectedItems);
