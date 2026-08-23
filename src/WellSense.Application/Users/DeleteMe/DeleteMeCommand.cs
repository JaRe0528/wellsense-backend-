using MediatR;

namespace WellSense.Application.Users.DeleteMe;

public record DeleteMeCommand(Guid CurrentUserId, string CurrentPassword) : IRequest<Unit>;
