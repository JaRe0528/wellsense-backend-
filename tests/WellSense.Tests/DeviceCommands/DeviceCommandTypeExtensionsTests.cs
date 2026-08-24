using FluentAssertions;
using WellSense.Domain.Devices;
using Xunit;

namespace WellSense.Tests.DeviceCommands;

public class DeviceCommandTypeExtensionsTests
{
    [Theory]
    [InlineData(DeviceCommandType.StartMonitoring, "START_MONITORING")]
    [InlineData(DeviceCommandType.StopMonitoring, "STOP_MONITORING")]
    [InlineData(DeviceCommandType.ChangeInterval, "CHANGE_INTERVAL")]
    [InlineData(DeviceCommandType.SyncNow, "SYNC_NOW")]
    [InlineData(DeviceCommandType.RequestStatus, "REQUEST_STATUS")]
    public void ToWireString_and_TryParseWireString_roundtrip_for_every_value(DeviceCommandType type, string wire)
    {
        type.ToWireString().Should().Be(wire);
        DeviceCommandTypeExtensions.TryParseWireString(wire, out var parsed).Should().BeTrue();
        parsed.Should().Be(type);
    }

    [Fact]
    public void TryParseWireString_rejects_unknown_values()
        => DeviceCommandTypeExtensions.TryParseWireString("NOT_A_REAL_TYPE", out _).Should().BeFalse();
}
