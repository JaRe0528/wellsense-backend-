using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.ChangePassword;

public class ChangePasswordCommandHandler(
    IWellSenseDbContext db,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock) : IRequestHandler<ChangePasswordCommand, Unit>
{
    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.CurrentUserId && !u.IsDeleted, ct)
            ?? throw AuthDomainException.InvalidCredentials();

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw AuthDomainException.InvalidCredentials();

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = clock.UtcNow;

        // Igual que en reset-password: cambiar la contraseña cierra todas las demás
        // sesiones activas (refresh tokens) por seguridad.
        var activeRefreshTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens)
            rt.RevokedAt = clock.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "password_changed",
            Metadata = "{}",
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
