using MediatR;

namespace WellSense.Application.Auth.Login;

public record LoginCommand(string Email, string Password, string? IpAddress) : IRequest<LoginResult>;

public record LoginResult(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, Guid UserId, string Email, bool EmailVerified);
