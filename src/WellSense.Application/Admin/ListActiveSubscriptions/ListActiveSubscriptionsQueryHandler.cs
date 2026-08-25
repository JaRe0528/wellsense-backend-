using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;

namespace WellSense.Application.Admin.ListActiveSubscriptions;

public class ListActiveSubscriptionsQueryHandler(IWellSenseDbContext db)
    : IRequestHandler<ListActiveSubscriptionsQuery, PagedResult<AdminSubscriptionListItem>>
{
    public async Task<PagedResult<AdminSubscriptionListItem>> Handle(ListActiveSubscriptionsQuery request, CancellationToken ct)
    {
        var query = db.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active);
        var totalCount = await query.CountAsync(ct);

        var subscriptions = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        if (subscriptions.Count == 0)
            return new PagedResult<AdminSubscriptionListItem>([], request.Page, request.PageSize, totalCount);

        var userIds = subscriptions.Select(s => s.UserId).Distinct().ToList();
        var planIds = subscriptions.Select(s => s.PlanId).Distinct().ToList();
        var userEmails = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email, ct);
        var plans = await db.MembershipPlans.Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var items = subscriptions
            .Select(s => new AdminSubscriptionListItem(
                s.Id,
                userEmails.GetValueOrDefault(s.UserId, "(usuario eliminado)"),
                plans[s.PlanId].Code.ToString().ToUpperInvariant(),
                s.StartedAt, s.EndsAt))
            .ToList();

        return new PagedResult<AdminSubscriptionListItem>(items, request.Page, request.PageSize, totalCount);
    }
}
