using MediatR;

namespace WellSense.Application.Auth.ResetPassword;

public record ResetPasswordCommand(string Token, string NewPassword) : IRequest<Unit>;
