using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.ForgotPassword;

/// <summary>
/// Siempre responde éxito (Unit), exista o no el email — nunca revelar por esta vía
/// qué correos están registrados (enumeration attack). Si el usuario existe y está
/// activo, se genera un token de un solo uso y se envía el correo.
///
/// Modificado (SMTP real, post-Bloque-10): se agregó una consulta a `Profiles` para
/// construir el nombre del saludo del correo ("Hola, {nombre}.") — a diferencia del
/// registro, un usuario que pide reset de contraseña normalmente YA tiene perfil. Si no
/// lo tiene (o no puso nombre), se manda null y SmtpEmailSender cae a usar el email.
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

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
            var recipientName = BuildDisplayName(profile?.FirstName, profile?.LastName);

            await emailSender.SendPasswordResetAsync(user.Email, recipientName, rawToken, ct);
        }

        return Unit.Value;
    }

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var name = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
