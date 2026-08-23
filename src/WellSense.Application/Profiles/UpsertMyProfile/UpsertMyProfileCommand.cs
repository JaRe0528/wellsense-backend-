using MediatR;

namespace WellSense.Application.Profiles.UpsertMyProfile;

public record UpsertMyProfileCommand(
    Guid CurrentUserId,
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    decimal? WeightKg,
    decimal? HeightCm,
    string? Occupation,
    string? AvatarUrl,
    string Timezone) : IRequest<Unit>;
