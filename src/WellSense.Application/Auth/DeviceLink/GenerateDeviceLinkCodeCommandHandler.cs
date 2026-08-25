using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Devices;
using WellSense.Domain.Identity;
using EntityState = Microsoft.EntityFrameworkCore.EntityState;

namespace WellSense.Application.Auth.DeviceLink;

/// <summary>
/// Genera un código de 6 dígitos para vincular el móvil, invocado desde la Web ya
/// autenticada. Dos invariantes de negocio de HANDOFF-DB (§8, riesgos 6 y 8):
///
/// 1) Debe invalidar (borrar) cualquier código no usado previo del mismo usuario en la
///    MISMA transacción que inserta el nuevo — el índice único parcial de la tabla solo
///    garantiza "no usado", no "no expirado", así que si no se borra explícitamente, un
///    código expirado pero no usado bloquearía la generación de uno nuevo.
/// 2) Si el código generado al azar colisiona con el hash de un código activo de OTRO
///    usuario (ux_device_link_codes_active_code_hash), se reintenta con un código nuevo
///    en vez de propagar el error al usuario — con solo 1,000,000 combinaciones posibles,
///    esto es raro pero no despreciable (paradoja del cumpleaños).
/// </summary>
public class GenerateDeviceLinkCodeCommandHandler(
    IWellSenseDbContext db,
    ITokenGenerator tokenGenerator,
    IDeviceLinkCodeHasher codeHasher,
    IUniqueConstraintViolationDetector violationDetector,
    IDateTimeProvider clock,
    ILogger<GenerateDeviceLinkCodeCommandHandler> logger) : IRequestHandler<GenerateDeviceLinkCodeCommand, GenerateDeviceLinkCodeResult>
{
    private const int MaxCollisionRetries = 5;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(30);

    public async Task<GenerateDeviceLinkCodeResult> Handle(GenerateDeviceLinkCodeCommand request, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxCollisionRetries; attempt++)
        {
            // Se re-consulta y re-borra en cada intento: si el intento anterior falló
            // por la violación de índice, el DbContext puede tener estado inconsistente
            // respecto de lo que ya se guardó (el DELETE si se alcanzó a confirmar antes
            // del fallo del INSERT en la misma llamada a SaveChanges NO se confirma, ya
            // que ambas operaciones viajan en la misma transacción implícita — por eso
            // es seguro repetir el DELETE en cada intento).
            var previousUnused = await db.DeviceLinkCodes
                .Where(c => c.UserId == request.CurrentUserId && c.UsedAt == null)
                .ToListAsync(ct);
            db.DeviceLinkCodes.RemoveRange(previousUnused);

            var rawCode = tokenGenerator.GenerateSixDigitCode();
            var now = clock.UtcNow;
            var newCode = new DeviceLinkCode
            {
                Id = Guid.NewGuid(),
                UserId = request.CurrentUserId,
                CodeHash = codeHasher.Hash(rawCode),
                Attempts = 0,
                MaxAttempts = 5,
                ExpiresAt = now.Add(CodeLifetime),
                CreatedAt = now
            };
            db.DeviceLinkCodes.Add(newCode);

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.CurrentUserId,
                Action = "device_link_code_generated",
                Metadata = "{}", // nunca el código en claro ni su hash — ver DeviceLinkCodeHasher
                CreatedAt = now
            };
            db.AuditLogs.Add(auditLog);

            try
            {
                // DELETE (invalidación del anterior) + INSERT (nuevo código) + el
                // registro de auditoría viajan en esta única llamada — misma
                // transacción implícita, tal como exige HANDOFF-DB.
                await db.SaveChangesAsync(ct);
                return new GenerateDeviceLinkCodeResult(rawCode, newCode.ExpiresAt);
            }
            catch (DbUpdateException ex) when (violationDetector.IsUniqueViolation(ex, "ux_device_link_codes_active_code_hash"))
            {
                logger.LogInformation(
                    "Colisión de código de vinculación en intento {Attempt} para user {UserId} — reintentando con otro código",
                    attempt, request.CurrentUserId);

                // EF Core NO revierte el change tracker automáticamente cuando falla
                // SaveChanges (solo la transacción de BD se revierte) — si no se
                // desprenden aquí las entidades de este intento fallido, el próximo
                // SaveChanges intentaría insertar/borrar de nuevo tanto lo viejo como
                // lo nuevo (incluyendo el registro de auditoría del intento fallido),
                // duplicando trabajo o fallando de otra forma.
                db.Entry(newCode).State = EntityState.Detached;
                db.Entry(auditLog).State = EntityState.Detached;
                foreach (var stale in previousUnused)
                    db.Entry(stale).State = EntityState.Unchanged;
            }
        }

        // Extremadamente improbable con 1,000,000 combinaciones y pocos códigos activos
        // a la vez, pero si se agotan los reintentos es mejor fallar explícito que
        // entrar en un loop infinito.
        throw new InvalidOperationException(
            "No se pudo generar un código de vinculación único tras varios intentos. Intenta de nuevo.");
    }
}
