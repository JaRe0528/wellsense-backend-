using MediatR;

namespace WellSense.Application.Auth.ChangePassword;

public record ChangePasswordCommand(Guid CurrentUserId, string CurrentPassword, string NewPassword) : IRequest<Unit>;
