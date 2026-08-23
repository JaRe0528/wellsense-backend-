using Microsoft.AspNetCore.SignalR;
using WellSense.Api.Hubs;
using WellSense.Application.Common.Interfaces;

namespace WellSense.Api.Services;

/// <summary>
/// Implementación de IDashboardNotifier (Application) usando SignalR (Api) — Application
/// nunca referencia Microsoft.AspNetCore.SignalR directamente, mismo principio que
/// ICurrentUserService/IEmailSender.
/// </summary>
public class SignalRDashboardNotifier(IHubContext<DashboardHub> hubContext) : IDashboardNotifier
{
    public Task NotifyUserAsync(Guid userId, string eventType, object payload, CancellationToken ct = default)
        => hubContext.Clients
            .Group(DashboardHub.GroupNameFor(userId))
            .SendAsync("dashboardUpdate", eventType, payload, ct);
}
