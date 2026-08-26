using MediatR;
using Microsoft.EntityFrameworkCore;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Application.ActivitySessions.ListMyActivitySessions;

/// <summary>Mismo criterio que ListMySleepSessionsQueryHandler — ventana simple "últimos N días" en UTC, sin límite de plan aplicado.</summary>
public class ListMyActivitySessionsQueryHandler(IWellSenseDbContext db, IDateTimeProvider clock)
    : IRequestHandler<ListMyActivitySessionsQuery, IReadOnlyList<ActivitySessionResult>>
{
    public async Task<IReadOnlyList<ActivitySessionResult>> Handle(ListMyActivitySessionsQuery request, CancellationToken ct)
    {
        var since = clock.UtcNow.AddDays(-request.Days);

        var sessions = await db.ActivitySessions
            .Where(a => a.UserId == request.CurrentUserId && a.StartAt >= since)
            .OrderByDescending(a => a.StartAt)
            .ToListAsync(ct);

        return sessions
            .Select(a => new ActivitySessionResult(a.Id, a.Type, a.StartAt, a.EndAt, a.Steps, a.DistanceM, a.Calories, a.CreatedAt))
            .ToList();
    }
}
