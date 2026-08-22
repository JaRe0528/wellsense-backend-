using MediatR;

namespace WellSense.Application.Auth.Logout;

public record LogoutCommand(Guid CurrentUserId, string RefreshToken) : IRequest<Unit>;
