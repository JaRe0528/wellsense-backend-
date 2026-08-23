using MediatR;

namespace WellSense.Application.Profiles.Onboarding.UpsertMySurvey;

public record UpsertMyOnboardingSurveyCommand(
    Guid CurrentUserId,
    string? UsualSchedule,
    string? SleepSchedule,
    string? DeclaredActivityLevel,
    string DeclaredStressLevel,
    string? DeclaredSleepQuality) : IRequest<Unit>;
