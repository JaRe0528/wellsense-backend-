using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.Notifications.ListMyNotifications;

public class ListMyNotificationsQueryHandler(IWellSenseDbContext db)
    : IRequestHandler<ListMyNotificationsQuery, IReadOnlyList<NotificationResult>>
{
    public async Task<IReadOnlyList<NotificationResult>> Handle(ListMyNotificationsQuery request, CancellationToken ct)
    {
        var query = db.Notifications.Where(n => n.UserId == request.CurrentUserId);
        if (request.UnreadOnly)
            query = query.Where(n => n.ReadAt == null);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationResult(n.Id, n.Type, n.Title, n.Body, n.ReadAt, n.CreatedAt))
            .ToListAsync(ct);
    }
}
