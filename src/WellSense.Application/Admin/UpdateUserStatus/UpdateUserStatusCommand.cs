using MediatR;

namespace WellSense.Application.Admin.UpdateUserStatus;

public record UpdateUserStatusCommand(Guid CurrentAdminUserId, Guid TargetUserId, string Status) : IRequest<Unit>;
