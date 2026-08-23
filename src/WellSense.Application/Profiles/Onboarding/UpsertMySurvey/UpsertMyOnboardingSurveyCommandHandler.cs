using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Profiles;

namespace WellSense.Application.Profiles.Onboarding.UpsertMySurvey;

/// <summary>
/// Upsert real, a diferencia de GetMySurvey que no auto-crea: aquí el usuario SÍ está
/// contestando la encuesta activamente, así que si el perfil todavía no existe se crea
/// de paso (get-or-create, mismo patrón que el resto del módulo), y si la encuesta ya
/// existía se sobreescribe — se permite recontestarla (ver decisión en HANDOFF: las
/// respuestas declaradas pueden cambiar con el tiempo, no tiene sentido congelarlas para
/// siempre en el primer envío).
/// </summary>
public class UpsertMyOnboardingSurveyCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<UpsertMyOnboardingSurveyCommand, Unit>
{
    public async Task<Unit> Handle(UpsertMyOnboardingSurveyCommand request, CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.CurrentUserId, ct);
        if (profile is null)
        {
            profile = new Profile
            {
                Id = Guid.NewGuid(), UserId = request.CurrentUserId, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
            };
            db.Profiles.Add(profile);
            await db.SaveChangesAsync(ct); // necesitamos profile.Id persistido antes del FK de la encuesta
        }

        var survey = await db.OnboardingSurveys.FirstOrDefaultAsync(s => s.ProfileId == profile.Id, ct);

        DeclaredStressLevelExtensions.TryParseWireString(request.DeclaredStressLevel, out var stressLevel);

        if (survey is null)
        {
            survey = new OnboardingSurvey { Id = Guid.NewGuid(), ProfileId = profile.Id, CreatedAt = clock.UtcNow };
            db.OnboardingSurveys.Add(survey);
        }

        survey.UsualSchedule = request.UsualSchedule;
        survey.SleepSchedule = request.SleepSchedule;
        survey.DeclaredActivityLevel = request.DeclaredActivityLevel;
        survey.DeclaredStressLevel = stressLevel;
        survey.DeclaredSleepQuality = request.DeclaredSleepQuality;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
