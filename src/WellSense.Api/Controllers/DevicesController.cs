using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Common.Interfaces;
using WellSense.Application.Devices.ListMyDevices;
using WellSense.Application.Devices.RegisterDevice;
using WellSense.Application.Devices.UnpairDevice;
using WellSense.Application.Devices.UpdateDeviceHeartbeat;

namespace WellSense.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DevicesController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> List(CancellationToken ct)
    {
        var devices = await mediator.Send(new ListMyDevicesQuery(currentUser.UserId!.Value), ct);
        return Ok(devices.Select(d => new DeviceResponse(
            d.Id, d.Type, d.Model, d.OsVersion, d.AppVersion, d.Status, d.LastSeenAt, d.PairedAt)));
    }

    /// <summary>
    /// Registra un dispositivo. En la práctica, el PHONE normalmente ya se creó vía
    /// device-link/redeem (Bloque 2) — este endpoint es principalmente para que el
    /// teléfono, ya autenticado, registre el WATCH que detectó emparejado por Wear OS.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RegisterDeviceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterDeviceResponse>> Register(RegisterDeviceRequest request, CancellationToken ct)
    {
        var id = await mediator.Send(new RegisterDeviceCommand(
            currentUser.UserId!.Value, request.Type, request.Model, request.OsVersion, request.AppVersion), ct);
        return CreatedAtAction(nameof(List), new RegisterDeviceResponse(id));
    }

    /// <summary>Heartbeat: reporta versión de app/OS actual y marca el dispositivo como visto ahora.</summary>
    [HttpPut("{deviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateHeartbeat(Guid deviceId, UpdateDeviceHeartbeatRequest request, CancellationToken ct)
    {
        await mediator.Send(new UpdateDeviceHeartbeatCommand(
            currentUser.UserId!.Value, deviceId, request.Model, request.OsVersion, request.AppVersion), ct);
        return NoContent();
    }

    /// <summary>Desvincula (soft — nunca borra el historial de mediciones de ese dispositivo).</summary>
    [HttpDelete("{deviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unpair(Guid deviceId, CancellationToken ct)
    {
        await mediator.Send(new UnpairDeviceCommand(currentUser.UserId!.Value, deviceId), ct);
        return NoContent();
    }
}
