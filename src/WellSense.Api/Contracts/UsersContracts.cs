namespace WellSense.Api.Contracts;

public record MeResponse(
    Guid Id,
    string Email,
    bool EmailVerified,
    string Role,
    string Status,
    DateTimeOffset CreatedAt);

public record DeleteMeRequest(string CurrentPassword);
