using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Identity;

namespace WellSense.Application.Admin.BootstrapFirstAdmin;

/// <summary>
/// Decisión de este bloque sobre cómo se promueve al primer admin (ver HANDOFF §1 para
/// la comparación completa de alternativas consideradas): un endpoint autoservicio,
/// protegido por DOS capas independientes, que se autodeshabilita después del primer uso:
///
/// 1) El llamador ya debe tener una cuenta y un Bearer válido (como cualquier endpoint
///    autenticado) — promueve SU PROPIA cuenta, nunca la de otro usuario.
/// 2) Un secreto compartido (`Admin:BootstrapSecret`, config, nunca versionado — mismo
///    patrón que Jwt:Secret/DeviceLink:Pepper) que solo quien tiene acceso a la
///    configuración del servidor conoce.
/// 3) Se autodeshabilita para siempre en cuanto existe CUALQUIER admin en el sistema —
///    ni siquiera hace falta revocar el secreto después: aunque alguien lo obtenga más
///    tarde, este camino ya no funciona.
///
/// Nunca lanza sin verificar el secreto primero, aunque ya existan admins — así no se
/// filtra por temporización/mensaje si el sistema ya está "bootstrapeado" a alguien que
/// ni siquiera tiene el secreto correcto.
/// </summary>
public class BootstrapFirstAdminCommandHandler(
    IWellSenseDbContext db, IConfiguration configuration, IDateTimeProvider clock)
    : IRequestHandler<BootstrapFirstAdminCommand, Unit>
{
    public async Task<Unit> Handle(BootstrapFirstAdminCommand request, CancellationToken ct)
    {
        var expectedSecret = configuration["Admin:BootstrapSecret"];
        if (string.IsNullOrWhiteSpace(expectedSecret) || request.Secret != expectedSecret)
            throw AdminDomainException.InvalidBootstrapSecret();

        var anyAdminExists = await db.Users.AnyAsync(u => u.Role == UserRole.Admin && !u.IsDeleted, ct);
        if (anyAdminExists)
            throw AdminDomainException.AlreadyBootstrapped();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.CurrentUserId && !u.IsDeleted, ct)
            ?? throw AdminDomainException.UserNotFound();

        user.Role = UserRole.Admin;
        user.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
