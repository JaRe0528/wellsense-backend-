using MediatR;

namespace WellSense.Application.Memberships.CancelSubscription;

public record CancelSubscriptionCommand(Guid CurrentUserId) : IRequest<Unit>;
