using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Auth.DeviceLink;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Api.Controllers;

/// <summary>
/// Flujo de vinculación de dispositivo móvil: el móvil JAMÁS usa email/password (ver
/// encargo del Bloque 2). "Generate" lo llama la Web ya autenticada; "Redeem" lo llama
/// el móvil sin ninguna credencial, solo el código de 6 dígitos.
/// </summary>
[ApiController]
[Route("api/v1/auth/device-link")]
public class DeviceLinkController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Requiere sesión web activa. Invalida cualquier código previo no usado del mismo usuario.</summary>
    [HttpPost("generate")]
    [Authorize]
    [ProducesResponseType(typeof(DeviceLinkCodeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeviceLinkCodeResponse>> Generate(CancellationToken ct)
    {
        var result = await mediator.Send(new GenerateDeviceLinkCodeCommand(currentUser.UserId!.Value), ct);
        return Ok(new DeviceLinkCodeResponse(result.Code, result.ExpiresAt));
    }

    /// <summary>
    /// Sin autenticación — el código ES la credencial. Rate limited de forma estricta
    /// por IP (P0, no negociable): ver IpRateLimiting:GeneralRules en appsettings para
    /// la ruta "*/auth/device-link/redeem". El contador `attempts` de la fila NO protege
    /// contra dígitos incorrectos al azar (no hay fila que atribuir el intento fallido si
    /// el hash no coincide con ninguna) — ver HANDOFF-DB §8 riesgo 7 y el HANDOFF de este
    /// bloque para el detalle completo de por qué la defensa real vive aquí, no en la BD.
    /// </summary>
    [HttpPost("redeem")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MobileAuthTokensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MobileAuthTokensResponse>> Redeem(RedeemDeviceLinkCodeRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RedeemDeviceLinkCodeCommand(
            request.Code, request.DeviceModel, request.OsVersion, request.AppVersion, currentUser.IpAddress), ct);

        return Ok(new MobileAuthTokensResponse(
            result.AccessToken, result.RefreshToken, result.AccessTokenExpiresAt,
            result.UserId, result.Email, result.DeviceId));
    }
}
