using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Devices;

namespace WellSense.Application.Devices.RegisterDevice;

/// <summary>
/// En la práctica, el PHONE normalmente se registra vía device-link/redeem (Bloque 2) —
/// este endpoint es principalmente para que el teléfono, ya autenticado, registre el
/// WATCH que detectó emparejado por Wear OS (Data Layer), ya que el reloj nunca habla
/// directo con el backend (ver 01-ARQUITECTURA-Y-STACK.md: Watch↔Phone es un canal
/// separado del backend). Se deja también disponible para PHONE por completitud/
/// simetría, no porque sea el camino esperado para ese caso.
///
/// Modificado en Bloque 10 (auditoría completa): se agregó un registro en `audit_logs`
/// (`device_registered`).
///
/// Modificado post-Bloque-10 (Parte 3: límites reales; Parte 5: tipo WEB):
/// - El parseo de `request.Type` ya NO es un ternario binario que caía silenciosamente
///   en WATCH para cualquier valor que no fuera "PHONE" (bug latente encontrado al
///   agregar WEB — nunca se manifestó porque el validador ya solo dejaba pasar PHONE/
///   WATCH, pero hubiera fallado en silencio en cuanto WEB llegara sin este fix).
/// - Se aplica el límite real de dispositivos del plan del usuario (403
///   PLAN_LIMIT_EXCEEDED) — solo cuentan los dispositivos NO desvinculados
///   (Status != Unpaired); desvincular uno libera el cupo.
/// </summary>
public class RegisterDeviceCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<RegisterDeviceCommand, Guid>
{
    public async Task<Guid> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        if (!TryParseType(request.Type, out var type))
            throw SyncDomainException.DeviceNotFound(); // no debería pasar — el validador ya lo cubre; defensivo, mismo criterio que IssueDeviceCommandCommandHandler

        var limits = await db.ResolveForUserAsync(request.CurrentUserId, ct);
        if (limits.MaxDevices is not null)
        {
            var currentDeviceCount = await db.Devices
                .CountAsync(d => d.UserId == request.CurrentUserId && d.Status != DeviceStatus.Unpaired, ct);
            if (currentDeviceCount >= limits.MaxDevices.Value)
                throw PaymentDomainException.PlanLimitExceeded($"{limits.MaxDevices.Value} dispositivo(s) vinculado(s)");
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            Type = type,
            Model = request.Model,
            OsVersion = request.OsVersion,
            AppVersion = request.AppVersion,
            Status = DeviceStatus.Active,
            LastSeenAt = clock.UtcNow,
            PairedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow
        };
        db.Devices.Add(device);

        db.AuditLogs.Add(new WellSense.Domain.Identity.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            Action = "device_registered",
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { deviceId = device.Id, type = request.Type }),
            CreatedAt = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return device.Id;
    }

    private static bool TryParseType(string wire, out DeviceType type)
    {
        switch (wire)
        {
            case "PHONE": type = DeviceType.Phone; return true;
            case "WATCH": type = DeviceType.Watch; return true;
            case "WEB": type = DeviceType.Web; return true;
            default: type = default; return false;
        }
    }
}
