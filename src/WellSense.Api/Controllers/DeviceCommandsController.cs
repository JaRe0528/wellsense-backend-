using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Devices.Commands.AcknowledgeDeviceCommand;
using WellSense.Application.Devices.Commands.IssueDeviceCommand;
using WellSense.Application.Devices.Commands.ListDeviceCommands;
using WellSense.Application.Devices.Commands.ListPendingDeviceCommands;

namespace WellSense.Api.Controllers;

/// <summary>
/// Web/Admin → API → SignalR (DeviceCommandHub) → Android → Watch — ver HANDOFF de este
/// bloque para el detalle completo del ciclo de vida PENDING→DELIVERED→ACKNOWLEDGED/
/// FAILED.
/// </summary>
[ApiController]
[Route("api/v1/devices/{deviceId:guid}/commands")]
[Authorize]
public class DeviceCommandsController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Emite un comando (Web/Admin). Se empuja por SignalR de mejor esfuerzo — siempre queda registrado aunque no haya nadie conectado.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IssueDeviceCommandResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IssueDeviceCommandResponse>> Issue(Guid deviceId, IssueDeviceCommandRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new IssueDeviceCommandCommand(
            currentUser.UserId!.Value, deviceId, request.Type, request.Payload), ct);

        return CreatedAtAction(nameof(List), new { deviceId },
            new IssueDeviceCommandResponse(result.CommandId, result.Type, result.Status, result.CreatedAt));
    }

    /// <summary>
    /// Confirma un comando (Android). Por REST, no por el mismo canal de SignalR que lo
    /// entregó — ver HANDOFF sobre por qué. Idempotente.
    /// </summary>
    [HttpPost("{commandId:guid}/ack")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ack(Guid deviceId, Guid commandId, AckDeviceCommandRequest request, CancellationToken ct)
    {
        await mediator.Send(new AcknowledgeDeviceCommandCommand(
            currentUser.UserId!.Value, deviceId, commandId, request.Status, request.AckPayload), ct);
        return NoContent();
    }

    /// <summary>Historial completo de comandos de un dispositivo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceCommandResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DeviceCommandResponse>>> List(Guid deviceId, CancellationToken ct)
    {
        var commands = await mediator.Send(new ListDeviceCommandsQuery(currentUser.UserId!.Value, deviceId), ct);
        return Ok(commands.Select(ToResponse));
    }

    /// <summary>Solo PENDING/DELIVERED — para que Android recupere lo que se haya perdido al reconectar (nunca depende solo del push en vivo).</summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceCommandResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DeviceCommandResponse>>> ListPending(Guid deviceId, CancellationToken ct)
    {
        var commands = await mediator.Send(new ListPendingDeviceCommandsQuery(currentUser.UserId!.Value, deviceId), ct);
        return Ok(commands.Select(ToResponse));
    }

    private static DeviceCommandResponse ToResponse(DeviceCommandResult c)
        => new(c.Id, c.Type, c.Payload, c.Status, c.AckPayload, c.CreatedAt, c.DeliveredAt, c.AcknowledgedAt, c.ExpiresAt);
}
