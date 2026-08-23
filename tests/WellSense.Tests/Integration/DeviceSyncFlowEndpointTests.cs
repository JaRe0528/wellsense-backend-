using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Bloque 4 (Devices + Measurements + Sync) end-to-end: registra/verifica/loguea un
/// usuario real, registra un dispositivo real, y sincroniza mediciones contra el
/// pipeline HTTP real — mismo patrón que AuthFlowEndpointTests/ProfileFlowEndpointTests.
/// </summary>
public class DeviceSyncFlowEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    private async Task<HttpClient> RegisterVerifyAndLoginAsync()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var token = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Register_device_then_sync_measurements_end_to_end()
    {
        var client = await RegisterVerifyAndLoginAsync();

        var registerResponse = await client.PostAsJsonAsync("/api/v1/devices",
            new RegisterDeviceRequest("WATCH", "Galaxy Watch 7", "Wear OS 5", "1.0.0"));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var device = await registerResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var syncRequest = new SyncMeasurementsRequest(device!.Id, $"batch-{Guid.NewGuid()}",
        [
            new SyncMeasurementItemRequest(Guid.NewGuid(), "HEART_RATE", 72, "bpm", DateTimeOffset.UtcNow.AddMinutes(-5)),
            new SyncMeasurementItemRequest(Guid.NewGuid(), "STEPS", 300, "steps", DateTimeOffset.UtcNow.AddMinutes(-2))
        ]);

        var syncResponse = await client.PostAsJsonAsync("/api/v1/sync/measurements", syncRequest);
        syncResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await syncResponse.Content.ReadFromJsonAsync<SyncMeasurementsResponse>();

        result!.AcceptedCount.Should().Be(2);
        result.Status.Should().Be("COMPLETED");
    }

    [Fact]
    public async Task Retrying_the_same_sync_request_over_http_is_idempotent()
    {
        var client = await RegisterVerifyAndLoginAsync();
        var registerResponse = await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("PHONE", null, null, null));
        var device = await registerResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var request = new SyncMeasurementsRequest(device!.Id, "http-idempotency-test",
        [
            new SyncMeasurementItemRequest(Guid.NewGuid(), "STEPS", 100, "steps", DateTimeOffset.UtcNow.AddMinutes(-1))
        ]);

        var first = await client.PostAsJsonAsync("/api/v1/sync/measurements", request);
        var second = await client.PostAsJsonAsync("/api/v1/sync/measurements", request); // reintento real por HTTP

        var firstResult = await first.Content.ReadFromJsonAsync<SyncMeasurementsResponse>();
        var secondResult = await second.Content.ReadFromJsonAsync<SyncMeasurementsResponse>();

        firstResult!.AcceptedCount.Should().Be(1);
        secondResult!.AcceptedCount.Should().Be(1); // mismo resultado, no se reprocesó
    }

    [Fact]
    public async Task Syncing_to_a_device_that_belongs_to_someone_else_returns_404()
    {
        var ownerClient = await RegisterVerifyAndLoginAsync();
        var registerResponse = await ownerClient.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("PHONE", null, null, null));
        var device = await registerResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var attackerClient = await RegisterVerifyAndLoginAsync();
        var request = new SyncMeasurementsRequest(device!.Id, "attempt-1",
        [
            new SyncMeasurementItemRequest(Guid.NewGuid(), "STEPS", 1, "steps", DateTimeOffset.UtcNow)
        ]);

        var response = await attackerClient.PostAsJsonAsync("/api/v1/sync/measurements", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unpaired_device_can_no_longer_sync()
    {
        var client = await RegisterVerifyAndLoginAsync();
        var registerResponse = await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest("WATCH", null, null, null));
        var device = await registerResponse.Content.ReadFromJsonAsync<RegisterDeviceResponse>();

        var unpairResponse = await client.DeleteAsync($"/api/v1/devices/{device!.Id}");
        unpairResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var syncResponse = await client.PostAsJsonAsync("/api/v1/sync/measurements", new SyncMeasurementsRequest(
            device.Id, "after-unpair",
            [new SyncMeasurementItemRequest(Guid.NewGuid(), "STEPS", 1, "steps", DateTimeOffset.UtcNow)]));

        syncResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
