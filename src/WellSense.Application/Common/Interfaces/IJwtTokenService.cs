namespace WellSense.Application.Common.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Access token de vida corta (15 min por defecto, ver Jwt:AccessTokenMinutes).</summary>
    string GenerateAccessToken(Guid userId, string email, string role);
}
