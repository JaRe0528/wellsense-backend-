using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Exceptions;
using WellSense.Application.Common.Interfaces;
using WellSense.Domain.Billing;

namespace WellSense.Application.Admin.GetUserDetail;

public class GetUserDetailQueryHandler(IWellSenseDbContext db) : IRequestHandler<GetUserDetailQuery, AdminUserDetail>
{
    public async Task<AdminUserDetail> Handle(GetUserDetailQuery request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId && !u.IsDeleted, ct)
            ?? throw AdminDomainException.UserNotFound();

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == request.TargetUserId, ct);
        var devices = await db.Devices.Where(d => d.UserId == request.TargetUserId).ToListAsync(ct);
        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == request.TargetUserId && s.Status == SubscriptionStatus.Active, ct);

        AdminSubscriptionSummary? subscriptionSummary = null;
        if (subscription is not null)
        {
            var plan = await db.MembershipPlans.FirstAsync(p => p.Id == subscription.PlanId, ct);
            subscriptionSummary = new AdminSubscriptionSummary(
                plan.Code.ToString().ToUpperInvariant(), subscription.Status.ToString().ToUpperInvariant(),
                subscription.StartedAt, subscription.EndsAt);
        }

        return new AdminUserDetail(
            user.Id, user.Email, user.EmailVerified, user.Role.ToString().ToUpperInvariant(), user.Status.ToString().ToUpperInvariant(), user.CreatedAt,
            profile is null ? null : new AdminProfileSummary(profile.FirstName, profile.LastName, profile.Timezone),
            devices.Select(d => new AdminDeviceSummary(d.Id, d.Type.ToString().ToUpperInvariant(), d.Status.ToString().ToUpperInvariant(), d.LastSeenAt)).ToList(),
            subscriptionSummary);
    }
}
