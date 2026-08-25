using MediatR;
using WellSense.Application.Common;

namespace WellSense.Application.Admin.ListActiveSubscriptions;

public record ListActiveSubscriptionsQuery(int Page, int PageSize) : IRequest<PagedResult<AdminSubscriptionListItem>>;

public record AdminSubscriptionListItem(
    Guid SubscriptionId, string UserEmail, string PlanCode, DateTimeOffset StartedAt, DateTimeOffset? EndsAt);
