using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Profiles;

namespace WellSense.Application.Profiles.Onboarding.GetMySurvey;

/// <summary>
/// A diferencia del perfil, NO se auto-crea: una encuesta de onboarding vacía no tiene
/// sentido (todos sus campos declarativos requieren que el usuario efectivamente la haya
/// contestado). Devuelve null (→ 204 en el controller) si todavía no existe.
/// </summary>
public class GetMyOnboardingSurveyQueryHandler(IWellSenseDbContext db)
    : IRequestHandler<GetMyOnboardingSurveyQuery, OnboardingSurveyResult?>
{
    public async Task<OnboardingSurveyResult?> Handle(GetMyOnboardingSurveyQuery request, CancellationToken ct)
    {
        var survey = await db.OnboardingSurveys
            .Where(s => db.Profiles.Any(p => p.Id == s.ProfileId && p.UserId == request.CurrentUserId))
            .FirstOrDefaultAsync(ct);

        if (survey is null) return null;

        return new OnboardingSurveyResult(
            survey.UsualSchedule, survey.SleepSchedule, survey.DeclaredActivityLevel,
            survey.DeclaredStressLevel.ToWireString(), survey.DeclaredSleepQuality, survey.CreatedAt);
    }
}
