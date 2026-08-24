using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Devices.Commands.Events;
using WellSense.Domain.Devices;

namespace WellSense.Application.Devices.Commands.AcknowledgeDeviceCommand;

/// <summary>
/// Deliberadamente por REST, no por el mismo canal de SignalR que entregó el comando —
/// decisión explícita de este bloque, ver HANDOFF: Android puede tardar en confirmar
/// (relaya al Watch, espera su respuesta), y la conexión de SignalR que recibió el
/// comando podría haberse caído y reconectado para entonces. Un POST autenticado normal
/// (con el mismo Bearer de siempre) es más resiliente/reintentable que depender de que
/// la MISMA conexión en vivo siga viva — por eso el ACK es REST, y solo el resultado ya
/// confirmado se reenvía a Web por SignalR (DeviceCommandAcknowledgedEvent).
///
/// Idempotente: reconocer un comando que ya estaba en un estado terminal
/// (ACKNOWLEDGED/FAILED) no lanza error ni lo vuelve a procesar — mismo criterio que
/// Logout/MarkNotificationRead en bloques anteriores, útil si Android reintenta el POST
/// por un timeout de red aunque el primero sí haya llegado.
/// </summary>
public class AcknowledgeDeviceCommandCommandHandler(
    IWellSenseDbContext db,
    IPublisher publisher,
    IDateTimeProvider clock) : IRequestHandler<AcknowledgeDeviceCommandCommand, Unit>
{
    public async Task<Unit> Handle(AcknowledgeDeviceCommandCommand request, CancellationToken ct)
    {
        var deviceExists = await db.Devices
            .AnyAsync(d => d.Id == request.DeviceId && d.UserId == request.CurrentUserId, ct);
        if (!deviceExists)
            throw SyncDomainException.DeviceNotFound();

        var command = await db.DeviceCommands
            .FirstOrDefaultAsync(c => c.Id == request.CommandId && c.DeviceId == request.DeviceId, ct)
            ?? throw SyncDomainException.CommandNotFound();

        if (command.Status is DeviceCommandStatus.Acknowledged or DeviceCommandStatus.Failed)
            return Unit.Value; // idempotente — ya se había confirmado, no se reprocesa

        command.Status = request.Status == "FAILED" ? DeviceCommandStatus.Failed : DeviceCommandStatus.Acknowledged;
        command.AcknowledgedAt = clock.UtcNow;
        command.AckPayload = request.AckPayloadJson;

        await db.SaveChangesAsync(ct);

        await publisher.Publish(new DeviceCommandAcknowledgedEvent(
            request.CurrentUserId, request.DeviceId, request.CommandId, request.Status), ct);

        return Unit.Value;
    }
}
