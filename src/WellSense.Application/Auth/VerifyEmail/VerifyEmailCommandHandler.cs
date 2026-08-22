using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Auth.VerifyEmail;

public class VerifyEmailCommandHandler(
    IWellSenseDbContext db,
    ITokenGenerator tokenGenerator,
    IDateTimeProvider clock) : IRequestHandler<VerifyEmailCommand, Unit>
{
    public async Task<Unit> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var hash = tokenGenerator.Sha256Hex(request.Token);

        var token = await db.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (token is null || token.UsedAt is not null || token.ExpiresAt <= clock.UtcNow)
            throw AuthDomainException.InvalidOrExpiredToken();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == token.UserId, ct)
            ?? throw AuthDomainException.InvalidOrExpiredToken();

        token.UsedAt = clock.UtcNow;
        user.EmailVerified = true;
        user.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
