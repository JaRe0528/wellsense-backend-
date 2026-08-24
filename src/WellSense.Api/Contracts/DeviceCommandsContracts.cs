namespace WellSense.Api.Contracts;

public record IssueDeviceCommandRequest(string Type, string? Payload);
public record IssueDeviceCommandResponse(Guid CommandId, string Type, string Status, DateTimeOffset CreatedAt);

public record AckDeviceCommandRequest(string Status, string? AckPayload);

public record DeviceCommandResponse(
    Guid Id,
    string Type,
    string Payload,
    string Status,
    string? AckPayload,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset ExpiresAt);
