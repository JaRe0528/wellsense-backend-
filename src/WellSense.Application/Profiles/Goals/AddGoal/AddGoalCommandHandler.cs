using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Profiles;

namespace WellSense.Application.Profiles.Goals.AddGoal;

/// <summary>
/// Igual que Upsert/GetMyProfile: si el perfil todavía no existe, se crea vacío de paso
/// (get-or-create) — un usuario no debería tener que "crear su perfil" antes de poder
/// fijarse una meta, son dos pantallas separadas en el cliente que no deberían estar
/// acopladas en ese orden.
/// </summary>
public class AddGoalCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<AddGoalCommand, Guid>
{
    public async Task<Guid> Handle(AddGoalCommand request, CancellationToken ct)
    {
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.CurrentUserId, ct);
        if (profile is null)
        {
            profile = new Profile { Id = Guid.NewGuid(), UserId = request.CurrentUserId, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow };
            db.Profiles.Add(profile);
        }

        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            ProfileId = profile.Id,
            Type = request.Type,
            TargetValue = request.TargetValue,
            CreatedAt = clock.UtcNow
        };
        db.Goals.Add(goal);

        await db.SaveChangesAsync(ct);
        return goal.Id;
    }
}
