using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Users.DeleteMe;

/// <summary>
/// Soft-delete (users.is_deleted/deleted_at, nunca DELETE físico — hay FKs desde
/// refresh_tokens, audit_logs, etc. que dependen de conservar la fila). Revoca todos
/// los refresh tokens activos, igual que en reset/change password — no hay razón para
/// dejar sesiones vivas de una cuenta que el propio usuario acaba de eliminar.
/// </summary>
public class DeleteMeCommandHandler(
    IWellSenseDbContext db,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock) : IRequestHandler<DeleteMeCommand, Unit>
{
    public async Task<Unit> Handle(DeleteMeCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.CurrentUserId && !u.IsDeleted, ct)
            ?? throw AuthDomainException.AccountNotFound();

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw AuthDomainException.InvalidCredentials();

        user.IsDeleted = true;
        user.DeletedAt = clock.UtcNow;
        user.UpdatedAt = clock.UtcNow;

        var activeRefreshTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var rt in activeRefreshTokens)
            rt.RevokedAt = clock.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "account_deleted",
            Metadata = "{}",
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
