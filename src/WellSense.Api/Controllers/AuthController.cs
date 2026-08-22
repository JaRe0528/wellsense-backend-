using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WellSense.Api.Contracts;
using WellSense.Application.Auth.ChangePassword;
using WellSense.Application.Auth.ForgotPassword;
using WellSense.Application.Auth.Login;
using WellSense.Application.Auth.Logout;
using WellSense.Application.Auth.TokenRefresh;
using WellSense.Application.Auth.Register;
using WellSense.Application.Auth.ResetPassword;
using WellSense.Application.Auth.VerifyEmail;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Api.Controllers;

/// <summary>
/// Auth para el flujo WEB: email/password. El flujo móvil (código de 6 dígitos) vive en
/// <see cref="DeviceLinkController"/> — son deliberadamente dos controladores distintos
/// porque, tal como pide el encargo, "login web y login móvil son flujos distintos".
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(ISender mediator, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Registro con email/password. No inicia sesión automáticamente: hay que verificar el correo primero.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterCommand(request.Email, request.Password), ct);
        return CreatedAtAction(nameof(Register), new RegisterResponse(
            result.UserId, result.Email, "Revisa tu correo para verificar la cuenta."));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await mediator.Send(new VerifyEmailCommand(request.Token), ct);
        return NoContent();
    }

    /// <summary>Login web. Rate limited — ver IpRateLimiting:GeneralRules en appsettings ("*/auth/login").</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokensResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthTokensResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(request.Email, request.Password, currentUser.IpAddress), ct);
        return Ok(new AuthTokensResponse(
            result.AccessToken, result.RefreshToken, result.AccessTokenExpiresAt, result.UserId, result.Email));
    }

    /// <summary>
    /// El endpoint de refresh no requiere Bearer válido (el propio refresh token ES la
    /// credencial), así que la respuesta no incluye UserId/Email — el cliente ya los
    /// tiene de su login/redención anterior.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RefreshResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new RefreshTokenCommand(request.RefreshToken, currentUser.IpAddress), ct);
        return Ok(new RefreshResponse(result.AccessToken, result.RefreshToken, result.AccessTokenExpiresAt));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await mediator.Send(new LogoutCommand(currentUser.UserId!.Value, request.RefreshToken), ct);
        return NoContent();
    }

    /// <summary>Rate limited. Siempre responde 204 exista o no el email — nunca reveles qué correos están registrados.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return NoContent();
    }

    /// <summary>Rate limited. Revoca todos los refresh tokens activos del usuario al resetear.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ResetPasswordCommand(request.Token, request.NewPassword), ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await mediator.Send(new ChangePasswordCommand(
            currentUser.UserId!.Value, request.CurrentPassword, request.NewPassword), ct);
        return NoContent();
    }
}
