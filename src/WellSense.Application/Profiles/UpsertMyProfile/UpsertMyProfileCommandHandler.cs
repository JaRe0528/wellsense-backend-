using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Profiles;

namespace WellSense.Application.Profiles.UpsertMyProfile;

/// <summary>
/// Upsert real (PUT, no PATCH): reemplaza todos los campos editables del perfil. Mismo
/// patrón get-or-create que GetMyProfileQueryHandler — si el perfil no existe todavía,
/// esta llamada también sirve para crearlo (no hace falta un endpoint de "crear perfil"
/// aparte).
/// </summary>
public class UpsertMyProfileCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<UpsertMyProfileCommand, Unit>
{
    public async Task<Unit> Handle(UpsertMyProfileCommand request, CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.CurrentUserId, ct);

        if (profile is null)
        {
            profile = new Profile { Id = Guid.NewGuid(), UserId = request.CurrentUserId, CreatedAt = clock.UtcNow };
            db.Profiles.Add(profile);
        }

        profile.FirstName = request.FirstName;
        profile.LastName = request.LastName;
        profile.BirthDate = request.BirthDate;
        profile.WeightKg = request.WeightKg;
        profile.HeightCm = request.HeightCm;
        profile.Occupation = request.Occupation;
        profile.AvatarUrl = request.AvatarUrl;
        profile.Timezone = request.Timezone;
        profile.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
