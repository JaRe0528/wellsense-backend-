using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Conecta un cliente SignalR REAL a DeviceCommandHub (como lo haría Android) — cubre la
/// plomería nueva de este bloque de punta a punta: el JWT vía query string acotado ahora
/// a DOS rutas de hub (ver Program.cs), `RegisterForDevice` verificando propiedad antes
/// de unir al grupo, y que un comando emitido por REST (como lo haría Web) realmente
/// llegue a un cliente Android conectado. Mismo patrón que DashboardHubEndpointTests
/// (Bloque 5).
/// </summary>
public class DeviceCommandHubEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    [Fact]
    public async Task Issuing_a_command_pushes_it_live_to_a_connected_device_registered_for_that_device_id()
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
        var deviceResponse = await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("WATCH", null, null, null));
        var device = await deviceResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/device-commands"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling; // TestServer no soporta upgrade real a WebSocket
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        Guid? receivedCommandId = null;
        var received = new TaskCompletionSource();
        hubConnection.On<object>("deviceCommand", payload =>
        {
            // El payload real es un objeto anónimo serializado — se relee como JSON
            // para no acoplar la prueba a la forma exacta de SignalRDeviceCommandNotifier.
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            receivedCommandId = doc.RootElement.GetProperty("commandId").GetGuid();
            received.TrySetResult();
        });

        await hubConnection.StartAsync();
        try
        {
            await hubConnection.InvokeAsync("RegisterForDevice", device!.Id);

            var issueResponse = await client.PostAsJsonAsync($"/api/v1/devices/{device.Id}/commands",
                new IssueDeviceCommandRequest("START_MONITORING", null));
            issueResponse.EnsureSuccessStatusCode();
            var issued = await issueResponse.Content.ReadFromJsonAsync<IssueDeviceCommandResponse>();

            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            completed.Should().Be(received.Task, "el dispositivo conectado y registrado debería recibir el comando en vivo");
            receivedCommandId.Should().Be(issued!.CommandId);
        }
        finally
        {
            await hubConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_connection_that_never_registers_for_the_device_does_not_receive_its_commands()
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
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/device-commands"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

        var receivedAnything = false;
        hubConnection.On<object>("deviceCommand", _ => receivedAnything = true);

        await hubConnection.StartAsync();
        try
        {
            // Deliberadamente NO se llama RegisterForDevice.
            await client.PostAsJsonAsync($"/api/v1/devices/{device!.Id}/commands", new IssueDeviceCommandRequest("SYNC_NOW", null));
            await Task.Delay(TimeSpan.FromSeconds(1)); // margen corto — no hay nada que esperar de verdad, solo confirmar que no llega

            receivedAnything.Should().BeFalse();
        }
        finally
        {
            await hubConnection.DisposeAsync();
        }
    }
}
