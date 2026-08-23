using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// La única prueba de este bloque que conecta un cliente SignalR real (no solo prueba el
/// handler que dispara el evento) — cubre exactamente la plomería nueva y riesgosa de
/// este bloque: el JWT vía query string (`access_token`) que Program.cs acota a
/// "/hubs/dashboard", el grupo por usuario en DashboardHub, y que
/// SignalRDashboardNotifier realmente llegue hasta un cliente conectado.
/// </summary>
public class DashboardHubEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    [Fact]
    public async Task Syncing_measurements_pushes_a_live_event_to_the_users_own_dashboard_connection()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var verificationToken = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(verificationToken));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        var accessToken = tokens!.AccessToken;

        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        var deviceResponse = await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("PHONE", null, null, null));
        var device = await deviceResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/dashboard"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                // TestServer no soporta un upgrade real a WebSocket — LongPolling usa
                // requests HTTP normales, que sí atraviesan el TestServer en memoria.
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        string? receivedEventType = null;
        var eventReceived = new TaskCompletionSource();
        hubConnection.On<string, object>("dashboardUpdate", (eventType, _) =>
        {
            receivedEventType = eventType;
            eventReceived.TrySetResult();
        });

        await hubConnection.StartAsync();
        try
        {
            var syncResponse = await client.PostAsJsonAsync("/api/v1/sync/measurements", new SyncMeasurementsRequest(
                device!.Id, $"hub-test-{Guid.NewGuid()}",
                [new SyncMeasurementItemRequest(Guid.NewGuid(), "STEPS", 100, "steps", DateTimeOffset.UtcNow.AddMinutes(-1))]));
            syncResponse.EnsureSuccessStatusCode();

            var completed = await Task.WhenAny(eventReceived.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.Should().Be(eventReceived.Task, "el dashboard conectado debería recibir el evento en vivo");
            receivedEventType.Should().Be("measurements_synced");
        }
        finally
        {
            await hubConnection.DisposeAsync();
        }
    }
}
