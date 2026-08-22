using System.Security.Claims;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private HttpContext? Context => httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var sub = Context?.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => Context?.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);

    public string? Role => Context?.User.FindFirstValue(ClaimTypes.Role);

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();
}
