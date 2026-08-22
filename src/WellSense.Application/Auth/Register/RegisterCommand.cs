using MediatR;

namespace WellSense.Application.Auth.Register;

public record RegisterCommand(string Email, string Password) : IRequest<RegisterResult>;

public record RegisterResult(Guid UserId, string Email);
