using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

public class NotificationsFlowEndpointTests(CustomWebApplicationFactory factory)
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

        var deviceResponse = await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("PHONE", null, null, null));
        var device = await deviceResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();
        return (client, device!.Id);
    }

    [Fact]
    public async Task Register_token_list_and_mark_read_flow()
    {
        var (client, deviceId) = await SetupUserWithDeviceAsync();

        var registerTokenResponse = await client.PostAsJsonAsync("/api/v1/notifications/tokens",
            new RegisterNotificationTokenRequest(deviceId, "fcm-token-e2e"));
        registerTokenResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var sendResponse = await client.PostAsJsonAsync("/api/v1/notifications/test",
            new SendTestNotificationRequest("Hola", "Este es un test"));
        sendResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sendResult = await sendResponse.Content.ReadFromJsonAsync<SendNotificationResponse>();

        var listResponse = await client.GetAsync("/api/v1/notifications");
        var notifications = await listResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        notifications.Should().ContainSingle(n => n.Id == sendResult!.NotificationId && n.ReadAt == null);

        var markReadResponse = await client.PutAsync($"/api/v1/notifications/{sendResult!.NotificationId}/read", null);
        markReadResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unreadResponse = await client.GetAsync("/api/v1/notifications?unreadOnly=true");
        var unread = await unreadResponse.Content.ReadFromJsonAsync<List<NotificationResponse>>();
        unread.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_token_for_device_of_another_user_returns_404()
    {
        var (_, deviceId) = await SetupUserWithDeviceAsync();
        var (attackerClient, _) = await SetupUserWithDeviceAsync();

        var response = await attackerClient.PostAsJsonAsync("/api/v1/notifications/tokens",
            new RegisterNotificationTokenRequest(deviceId, "fcm-token-attack"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_web_device_registers_and_receives_a_notification_token_exactly_like_phone_or_watch()
    {
        // Parte 5 del encargo: confirmar explícitamente que WEB funciona igual de bien
        // que PHONE/WATCH en ambos endpoints, sin lógica nueva — solo el tipo permitido.
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var token = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);

        var deviceResponse = await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("WEB", "Chrome 128", null, null));
        deviceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var device = await deviceResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var tokenResponse = await client.PostAsJsonAsync("/api/v1/notifications/tokens",
            new RegisterNotificationTokenRequest(device!.Id, "web-push-token-abc"));

        tokenResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
