using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

public class DeviceCommandsFlowEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    private async Task<(HttpClient client, Guid deviceId)> SetupUserWithDeviceAsync()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var token = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);

        var deviceResponse = await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("WATCH", null, null, null));
        var device = await deviceResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        return (client, device!.Id);
    }

    [Fact]
    public async Task Full_issue_list_ack_cycle_over_http()
    {
        var (client, deviceId) = await SetupUserWithDeviceAsync();

        var issueResponse = await client.PostAsJsonAsync($"/api/v1/devices/{deviceId}/commands",
            new IssueDeviceCommandRequest("CHANGE_INTERVAL", "{\"intervalSeconds\":30}"));
        issueResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var issued = await issueResponse.Content.ReadFromJsonAsync<IssueDeviceCommandResponse>();

        var pendingResponse = await client.GetAsync($"/api/v1/devices/{deviceId}/commands/pending");
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<DeviceCommandResponse>>();
        pending.Should().ContainSingle(c => c.Id == issued!.CommandId);

        var ackResponse = await client.PostAsJsonAsync(
            $"/api/v1/devices/{deviceId}/commands/{issued!.CommandId}/ack",
            new AckDeviceCommandRequest("ACKNOWLEDGED", "{\"applied\":true}"));
        ackResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var historyResponse = await client.GetAsync($"/api/v1/devices/{deviceId}/commands");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DeviceCommandResponse>>();
        history.Should().ContainSingle(c => c.Id == issued.CommandId && c.Status == "ACKNOWLEDGED");

        var pendingAfterAck = await client.GetAsync($"/api/v1/devices/{deviceId}/commands/pending");
        var pendingAfter = await pendingAfterAck.Content.ReadFromJsonAsync<List<DeviceCommandResponse>>();
        pendingAfter.Should().BeEmpty(); // ya no está pendiente
    }

    [Fact]
    public async Task Change_interval_without_a_valid_payload_returns_400()
    {
        var (client, deviceId) = await SetupUserWithDeviceAsync();

        var response = await client.PostAsJsonAsync($"/api/v1/devices/{deviceId}/commands",
            new IssueDeviceCommandRequest("CHANGE_INTERVAL", null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Issuing_to_a_device_of_another_user_returns_404()
    {
        var (_, deviceId) = await SetupUserWithDeviceAsync();
        var (attackerClient, _) = await SetupUserWithDeviceAsync();

        var response = await attackerClient.PostAsJsonAsync($"/api/v1/devices/{deviceId}/commands",
            new IssueDeviceCommandRequest("SYNC_NOW", null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var client = factory.CreateClient();
        var deviceId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/v1/devices/{deviceId}/commands",
            new IssueDeviceCommandRequest("SYNC_NOW", null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
