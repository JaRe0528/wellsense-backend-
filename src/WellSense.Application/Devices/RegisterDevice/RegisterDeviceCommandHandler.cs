using MediatR;
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
/// (`device_registered`) — no se registraba antes.
/// </summary>
public class RegisterDeviceCommandHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<RegisterDeviceCommand, Guid>
{
    public async Task<Guid> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            UserId = request.CurrentUserId,
            Type = request.Type == "WATCH" ? DeviceType.Watch : DeviceType.Phone,
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
}
