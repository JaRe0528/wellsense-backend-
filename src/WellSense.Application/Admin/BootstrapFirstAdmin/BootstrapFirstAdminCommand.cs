using MediatR;

namespace WellSense.Application.Admin.BootstrapFirstAdmin;

public record BootstrapFirstAdminCommand(Guid CurrentUserId, string Secret) : IRequest<Unit>;
