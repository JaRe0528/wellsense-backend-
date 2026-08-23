using FluentValidation;
using WellSense.Domain.Profiles;

namespace WellSense.Application.Profiles.Onboarding.UpsertMySurvey;

public class UpsertMyOnboardingSurveyCommandValidator : AbstractValidator<UpsertMyOnboardingSurveyCommand>
{
    public UpsertMyOnboardingSurveyCommandValidator()
    {
        RuleFor(x => x.DeclaredStressLevel)
            .NotEmpty()
            .Must(v => DeclaredStressLevelExtensions.TryParseWireString(v, out _))
            .WithMessage("declaredStressLevel debe ser uno de: MUY_BAJO, BAJO, MODERADO, ALTO, MUY_ALTO.");

        RuleFor(x => x.UsualSchedule).MaximumLength(200);
        RuleFor(x => x.SleepSchedule).MaximumLength(200);
        RuleFor(x => x.DeclaredActivityLevel).MaximumLength(50);
        RuleFor(x => x.DeclaredSleepQuality).MaximumLength(50);
    }
}
