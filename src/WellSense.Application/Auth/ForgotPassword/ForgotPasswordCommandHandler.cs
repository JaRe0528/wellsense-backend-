using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.ForgotPassword;

/// <summary>
/// Siempre responde éxito (Unit), exista o no el email — nunca revelar por esta vía
/// qué correos están registrados (enumeration attack). Si el usuario existe y está
/// activo, se genera un token de un solo uso y se "envía" el correo.
/// </summary>
public class ForgotPasswordCommandHandler(
    IWellSenseDbContext db,
    ITokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IDateTimeProvider clock) : IRequestHandler<ForgotPasswordCommand, Unit>
{
    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(
            u => !u.IsDeleted && u.Status == UserStatus.Active && u.Email.ToLower() == normalizedEmail, ct);

        if (user is not null)
        {
            var rawToken = tokenGenerator.GenerateUrlSafeToken();
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenGenerator.Sha256Hex(rawToken),
                ExpiresAt = clock.UtcNow.AddHours(1)
            });
            await db.SaveChangesAsync(ct);
            await emailSender.SendPasswordResetAsync(user.Email, rawToken, ct);
        }

        return Unit.Value;
    }
}
