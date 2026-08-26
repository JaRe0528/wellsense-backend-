using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WellSense.Api.Contracts;
using WellSense.Domain.Measurements;
using WellSense.Infrastructure.Persistence;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Fix urgente: Web asumía GET /sync?type=sleep/activity, que nunca existió. No hay
/// ningún endpoint que ESCRIBA en sleep_sessions/activity_sessions todavía (a diferencia
/// de measurements, que sí tiene /sync/measurements) — se siembra directo contra el
/// mismo store InMemory de la app real vía `factory.Services`, mismo patrón ya usado y
/// corregido en el fix de Bloque 9 (nunca un DbContext armado a mano con un
/// DbContextOptionsBuilder aparte, que crearía un store aislado con el mismo nombre pero
/// sin conexión real al de la app).
/// </summary>
public class SleepAndActivityFlowEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    private async Task<(HttpClient client, Guid userId)> RegisterVerifyAndLoginAsync()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        var token = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
        return (client, registerResult!.UserId);
    }

    [Fact]
    public async Task Sleep_sessions_endpoint_returns_the_users_own_sessions_over_http()
    {
        var (client, userId) = await RegisterVerifyAndLoginAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WellSenseDbContext>();
            db.SleepSessions.Add(new SleepSession
            {
                Id = Guid.NewGuid(), UserId = userId,
                StartAt = DateTimeOffset.UtcNow.AddHours(-8), EndAt = DateTimeOffset.UtcNow,
                Stages = "{\"deep\":90,\"rem\":60,\"light\":330}", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/sleep-sessions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<List<SleepSessionResponse>>();
        sessions.Should().ContainSingle();
        sessions![0].Stages.Should().Contain("deep");
    }

    [Fact]
    public async Task Activity_sessions_endpoint_respects_the_days_query_parameter()
    {
        var (client, userId) = await RegisterVerifyAndLoginAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WellSenseDbContext>();
            db.ActivitySessions.Add(new ActivitySession // dentro de 7 días
            {
                Id = Guid.NewGuid(), UserId = userId, Type = "RUNNING",
                StartAt = DateTimeOffset.UtcNow.AddDays(-2).AddMinutes(-30), EndAt = DateTimeOffset.UtcNow.AddDays(-2),
                Steps = 4500, DistanceM = 3800m, Calories = 250m, CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
            });
            db.ActivitySessions.Add(new ActivitySession // fuera de 7 días
            {
                Id = Guid.NewGuid(), UserId = userId, Type = "CYCLING",
                StartAt = DateTimeOffset.UtcNow.AddDays(-20).AddMinutes(-30), EndAt = DateTimeOffset.UtcNow.AddDays(-20),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-20)
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/activity-sessions?days=7");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sessions = await response.Content.ReadFromJsonAsync<List<ActivitySessionResponse>>();
        sessions.Should().ContainSingle(s => s.Type == "RUNNING");
    }

    [Fact]
    public async Task Both_endpoints_require_authentication_and_never_leak_other_users_sessions()
    {
        var unauthClient = factory.CreateClient();
        var sleepResponse = await unauthClient.GetAsync("/api/v1/sleep-sessions");
        var activityResponse = await unauthClient.GetAsync("/api/v1/activity-sessions");
        sleepResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        activityResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, ownerId) = await RegisterVerifyAndLoginAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WellSenseDbContext>();
            db.SleepSessions.Add(new SleepSession
            {
                Id = Guid.NewGuid(), UserId = ownerId,
                StartAt = DateTimeOffset.UtcNow.AddHours(-8), EndAt = DateTimeOffset.UtcNow,
                Stages = "{}", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var (otherClient, _) = await RegisterVerifyAndLoginAsync();
        var otherResponse = await otherClient.GetAsync("/api/v1/sleep-sessions");
        var otherSessions = await otherResponse.Content.ReadFromJsonAsync<List<SleepSessionResponse>>();
        otherSessions.Should().BeEmpty(); // nunca ve las sesiones del otro usuario
    }
}
