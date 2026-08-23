using MediatR;

namespace WellSense.Application.Profiles.GetMyProfile;

public record GetMyProfileQuery(Guid CurrentUserId) : IRequest<ProfileResult>;

public record ProfileResult(
    Guid Id,
    string? FirstName,
    string? LastName,
    DateOnly? BirthDate,
    decimal? WeightKg,
    decimal? HeightCm,
    string? Occupation,
    string? AvatarUrl,
    string Timezone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
