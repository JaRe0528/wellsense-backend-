using MediatR;

namespace WellSense.Application.Auth.TokenRefresh;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<RefreshTokenResult>;

public record RefreshTokenResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt);
