using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Admin.UpdateUserStatus;

/// <summary>
/// Suspender revoca TODAS las sesiones activas (refresh tokens) del usuario, mismo
/// patrón que reset/change password (Bloque 2) y DeleteMe (Bloque 3) — de lo contrario
/// un usuario recién suspendido podría seguir usando un refresh token ya emitido para
/// obtener access tokens nuevos indefinidamente, dejando la suspensión sin efecto real.
/// Reactivar NO restaura sesiones — el usuario simplemente vuelve a poder iniciar
/// sesión.
///
/// Guardia explícita: un admin no puede suspenderse a sí mismo (evita quedar
/// autobloqueado sin otro admin que lo reactive).
/// </summary>
public class UpdateUserStatusCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<UpdateUserStatusCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUserStatusCommand request, CancellationToken ct)
    {
        if (request.TargetUserId == request.CurrentAdminUserId && request.Status == "SUSPENDED")
            throw AdminDomainException.CannotSuspendSelf();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId && !u.IsDeleted, ct)
            ?? throw AdminDomainException.UserNotFound();

        var newStatus = request.Status == "SUSPENDED" ? UserStatus.Suspended : UserStatus.Active;
        user.Status = newStatus;
        user.UpdatedAt = clock.UtcNow;

        if (newStatus == UserStatus.Suspended)
        {
            var activeRefreshTokens = await db.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var rt in activeRefreshTokens)
                rt.RevokedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
