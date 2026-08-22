using MediatR;

namespace WellSense.Application.Auth.VerifyEmail;

public record VerifyEmailCommand(string Token) : IRequest<Unit>;
