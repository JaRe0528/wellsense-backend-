using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// End-to-end real de Bloque 7: sincroniza mediciones reales vía /sync/measurements
/// (Bloque 4) y confirma que /wellness/compute (este bloque) las encuentra y calcula un
/// puntaje — cubre la integración entre bloques, no solo la lógica de este en
/// aislamiento.
/// </summary>
public class WellnessFlowEndpointTests(CustomWebApplicationFactory factory)
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
    public async Task Get_my_scores_before_any_data_returns_nulls_not_an_error()
    {
        var (client, _) = await SetupUserWithDeviceAsync();

        var response = await client.GetAsync("/api/v1/wellness/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var scores = await response.Content.ReadFromJsonAsync<DailyScoresResponse>();
        scores!.WellnessScore.Should().BeNull();
    }

    [Fact]
    public async Task Compute_without_any_synced_data_returns_400_insufficient_data()
    {
        var (client, _) = await SetupUserWithDeviceAsync();

        var response = await client.PostAsJsonAsync("/api/v1/wellness/compute", new ComputeScoresRequest(new DateOnly(2020, 1, 1)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Syncing_measurements_then_computing_produces_a_real_wellness_score()
    {
        var (client, deviceId) = await SetupUserWithDeviceAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var syncResponse = await client.PostAsJsonAsync("/api/v1/sync/measurements", new SyncMeasurementsRequest(
            deviceId, $"batch-{Guid.NewGuid()}",
            [
                new SyncMeasurementItemRequest(Guid.NewGuid(), "STEPS", 6000, "steps", DateTimeOffset.UtcNow.AddHours(-2)),
                new SyncMeasurementItemRequest(Guid.NewGuid(), "HEART_RATE", 68, "bpm", DateTimeOffset.UtcNow.AddHours(-1))
            ]));
        syncResponse.EnsureSuccessStatusCode();

        var computeResponse = await client.PostAsJsonAsync("/api/v1/wellness/compute", new ComputeScoresRequest(today));
        computeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var computed = await computeResponse.Content.ReadFromJsonAsync<ComputeScoresResponse>();
        computed!.Wellness.Should().NotBeNull();
        computed.Wellness!.Score.Should().Be(60m); // 6000 pasos = componente de actividad 60, único disponible

        var getResponse = await client.GetAsync($"/api/v1/wellness/me?date={today:yyyy-MM-dd}");
        var scores = await getResponse.Content.ReadFromJsonAsync<DailyScoresResponse>();
        scores!.WellnessScore.Should().Be(60m);

        var historyResponse = await client.GetAsync("/api/v1/wellness/me/history?days=7");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<DailyScoreHistoryItemResponse>>();
        history.Should().ContainSingle(h => h.Date == today && h.WellnessScore == 60m);
    }

    [Fact]
    public async Task Requires_authentication()
    {
        var client = factory.CreateClient();

        var meResponse = await client.GetAsync("/api/v1/wellness/me");
        var computeResponse = await client.PostAsJsonAsync("/api/v1/wellness/compute", new ComputeScoresRequest(null));

        meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        computeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
