using MediatR;

namespace WellSense.Application.Auth.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<Unit>;
