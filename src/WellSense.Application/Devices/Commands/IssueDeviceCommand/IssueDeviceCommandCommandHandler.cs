using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Devices;

namespace WellSense.Application.Devices.Commands.IssueDeviceCommand;

/// <summary>
/// Web/Admin → API → SignalR → Android → Watch (ver 01-ARQUITECTURA-Y-STACK.md, flujo
/// inverso de comandos). El registro en `device_commands` con status PENDING SIEMPRE se
/// crea, exista o no un cliente Android conectado en ese momento — el push por SignalR
/// es un mejor-esfuerzo de entrega inmediata, nunca la fuente de verdad de si el comando
/// "existe". Si el push falla o no hay nadie conectado, el comando queda PENDING y
/// Android puede recuperarlo después vía ListPendingDeviceCommands al reconectar — mismo
/// principio de "el monitoreo nunca depende de Internet" del documento de arquitectura.
/// </summary>
public class IssueDeviceCommandCommandHandler(
    IWellSenseDbContext db,
    IDeviceCommandNotifier notifier,
    IDateTimeProvider clock,
    ILogger<IssueDeviceCommandCommandHandler> logger) : IRequestHandler<IssueDeviceCommandCommand, IssueDeviceCommandResult>
{
    public async Task<IssueDeviceCommandResult> Handle(IssueDeviceCommandCommand request, CancellationToken ct)
    {
        var device = await db.Devices
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct)
            ?? throw SyncDomainException.DeviceNotFound();
        if (device.Status == DeviceStatus.Unpaired)
            throw SyncDomainException.DeviceNotFound(); // mismo error genérico — un dispositivo desvinculado no debe poder recibir comandos

        if (!DeviceCommandTypeExtensions.TryParseWireString(request.Type, out var type))
            throw SyncDomainException.DeviceNotFound(); // no debería pasar — el validador ya lo cubre; defensivo

        var now = clock.UtcNow;
        var command = new DeviceCommand
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            UserId = request.CurrentUserId,
            Type = type,
            Payload = request.PayloadJson ?? "{}",
            Status = DeviceCommandStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };
        db.DeviceCommands.Add(command);
        await db.SaveChangesAsync(ct);

        try
        {
            await notifier.NotifyDeviceAsync(request.DeviceId, new
            {
                commandId = command.Id,
                type = type.ToWireString(),
                payload = command.Payload
            }, ct);

            // "Delivered" acá significa "se intentó empujar por SignalR", NO que Android
            // efectivamente lo recibió — esa confirmación real es el ACK, un paso
            // completamente aparte (ver AcknowledgeDeviceCommandCommandHandler).
            command.Status = DeviceCommandStatus.Delivered;
            command.DeliveredAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Nunca debe tumbar la creación del comando — el comando ya se guardó como
            // PENDING y sigue siendo válido; Android lo recuperará vía polling si el push
            // en vivo falló.
            logger.LogWarning(ex, "No se pudo empujar el comando {CommandId} por SignalR — queda PENDING.", command.Id);
        }

        return new IssueDeviceCommandResult(command.Id, type.ToWireString(), command.Status.ToString().ToUpperInvariant(), command.CreatedAt);
    }
}
