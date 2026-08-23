using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Profiles;

namespace WellSense.Application.Profiles.GetMyProfile;

/// <summary>
/// Get-or-create perezoso: la primera vez que un usuario pide su perfil (o lo edita),
/// si la fila de `profiles` todavía no existe se crea vacía en ese momento — en vez de
/// crearla en el registro (Bloque 2, ya cerrado y aprobado, no se toca) o exigir un
/// endpoint explícito de "crear perfil" antes de poder verlo. Así Web/Android nunca
/// tienen que manejar un caso especial de "perfil no existe todavía": GET siempre
/// devuelve algo, aunque sea todo null salvo timezone="UTC".
/// </summary>
public class GetMyProfileQueryHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<GetMyProfileQuery, ProfileResult>
{
    public async Task<ProfileResult> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.CurrentUserId, ct);

        if (profile is null)
        {
            profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = request.CurrentUserId,
                Timezone = "UTC",
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow
            };
            db.Profiles.Add(profile);
            await db.SaveChangesAsync(ct);
        }

        return new ProfileResult(
            profile.Id, profile.FirstName, profile.LastName, profile.BirthDate, profile.WeightKg,
            profile.HeightCm, profile.Occupation, profile.AvatarUrl, profile.Timezone,
            profile.CreatedAt, profile.UpdatedAt);
    }
}
