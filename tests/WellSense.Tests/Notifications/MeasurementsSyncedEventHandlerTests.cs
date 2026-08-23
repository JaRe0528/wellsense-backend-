using FluentAssertions;
using WellSense.Application.Notifications.Events;
using WellSense.Tests.TestHelpers;
using Xunit;

namespace WellSense.Tests.Notifications;

public class MeasurementsSyncedEventHandlerTests
{
    [Fact]
    public async Task Handle_forwards_to_dashboard_notifier_with_stable_event_type()
    {
        var notifier = new SpyDashboardNotifier();
        var handler = new MeasurementsSyncedEventHandler(notifier);
        var userId = Guid.NewGuid();
        var syncedAt = DateTimeOffset.UtcNow;

        await handler.Handle(new MeasurementsSyncedEvent(userId, 5, syncedAt), default);

        notifier.Calls.Should().ContainSingle();
        var call = notifier.Calls.Single();
        call.UserId.Should().Be(userId);
        call.EventType.Should().Be("measurements_synced");
    }
}
