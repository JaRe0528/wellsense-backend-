using MediatR;

namespace WellSense.Application.Profiles.Onboarding.GetMySurvey;

public record GetMyOnboardingSurveyQuery(Guid CurrentUserId) : IRequest<OnboardingSurveyResult?>;

public record OnboardingSurveyResult(
    string? UsualSchedule,
    string? SleepSchedule,
    string? DeclaredActivityLevel,
    string DeclaredStressLevel,
    string? DeclaredSleepQuality,
    DateTimeOffset CreatedAt);
