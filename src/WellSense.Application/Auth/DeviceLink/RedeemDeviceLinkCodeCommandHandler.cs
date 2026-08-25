using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;

namespace WellSense.Application.Auth.DeviceLink;

/// <summary>
/// Redención del código de 6 dígitos por el móvil, SIN credenciales. La defensa contra
/// fuerza bruta de este endpoint NO es esta clase — el `code_hash` identifica a lo sumo
/// una fila activa (por diseño de `ux_device_link_codes_active_code_hash`, ver
/// HANDOFF-DB), así que un código mal tecleado nunca produce una fila que "contar" en
/// `attempts` (HANDOFF-DB §8 riesgo 7). La defensa real es el rate limiting por IP
/// configurado a nivel de endpoint en la Api (ver Program.cs / appsettings
/// IpRateLimiting) — P0 según el encargo de ese bloque.
///
/// Modificado en Bloque 10 (auditoría completa): se agregó un registro en `audit_logs`
/// (`device_link_code_redeemed`) al final del camino feliz — nunca en los caminos de
/// error (código inválido/expirado/bloqueado), donde no hay a quién atribuir el intento
/// de forma confiable sin arriesgar la misma fuga de información que ya evita
/// AuthDomainException.InvalidDeviceLinkCode() en la respuesta HTTP.
/// </summary>
public class RedeemDeviceLinkCodeCommandHandler(
    IWellSenseDbContext db,
    IDeviceLinkCodeHasher codeHasher,
    IJwtTokenService jwtTokenService,
    ITokenGenerator tokenGenerator,
    IDateTimeProvider clock,
    IConfiguration configuration) : IRequestHandler<RedeemDeviceLinkCodeCommand, RedeemDeviceLinkCodeResult>
{
    public async Task<RedeemDeviceLinkCodeResult> Handle(RedeemDeviceLinkCodeCommand request, CancellationToken ct)
    {
        var hash = codeHasher.Hash(request.Code);

        var code = await db.DeviceLinkCodes.FirstOrDefaultAsync(c => c.CodeHash == hash && c.UsedAt == null, ct);

        // Mismo error para "no existe ninguna fila con este hash" y para "expiró" —
        // no dar pistas de cuál fue el motivo exacto del rechazo.
        if (code is null || code.ExpiresAt <= clock.UtcNow)
            throw AuthDomainException.InvalidDeviceLinkCode();

        if (code.Attempts >= code.MaxAttempts)
            throw AuthDomainException.DeviceLinkCodeLocked();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == code.UserId && !u.IsDeleted, ct);
        if (user is null || user.Status != UserStatus.Active)
            throw AuthDomainException.InvalidDeviceLinkCode();

        var device = new Device
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Type = DeviceType.Phone,
            Model = request.DeviceModel,
            OsVersion = request.OsVersion,
            AppVersion = request.AppVersion,
            Status = DeviceStatus.Active,
            LastSeenAt = clock.UtcNow,
            PairedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        db.Devices.Add(device);

        code.UsedAt = clock.UtcNow;
        code.DeviceId = device.Id; // invariante (used_at IS NULL) = (device_id IS NULL) queda satisfecha

        var refreshDays = configuration.GetValue("Jwt:RefreshTokenDays", 30);
        var accessMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);

        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, user.Email, user.Role.ToString());
        var rawRefreshToken = tokenGenerator.GenerateUrlSafeToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenGenerator.Sha256Hex(rawRefreshToken),
            ExpiresAt = clock.UtcNow.AddDays(refreshDays),
            CreatedByIp = request.IpAddress,
            CreatedAt = clock.UtcNow
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Action = "device_link_code_redeemed",
            Metadata = "{}",
            IpAddress = request.IpAddress,
            CreatedAt = clock.UtcNow
        });

        // Device (creado), DeviceLinkCode (marcado usado), RefreshToken (nuevo) y el
        // registro de auditoría se confirman juntos en una sola transacción implícita.
        await db.SaveChangesAsync(ct);

        return new RedeemDeviceLinkCodeResult(
            accessToken, rawRefreshToken, clock.UtcNow.AddMinutes(accessMinutes), user.Id, user.Email, device.Id);
    }
}
