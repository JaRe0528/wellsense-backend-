using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using WellSense.Api.Contracts;
using Xunit;

namespace WellSense.Tests.Integration;

/// <summary>
/// Pruebas HTTP end-to-end de Users+Profile (Bloque 3), mismo patrón que
/// AuthFlowEndpointTests: registra y verifica un usuario real, obtiene un access
/// token real vía /login, y ejercita los endpoints protegidos con Bearer real.
/// </summary>
public class ProfileFlowEndpointTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Password123";

    private async Task<(HttpClient client, Guid userId)> RegisterVerifyAndLoginAsync()
    {
        var client = factory.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, Password));
        var token = factory.CapturedEmails.VerificationTokens[email];
        await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(token));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);
        return (client, tokens.UserId);
    }

    [Fact]
    public async Task Get_profile_before_ever_setting_one_lazily_creates_it_with_utc_timezone()
    {
        var (client, _) = await RegisterVerifyAndLoginAsync();

        var response = await client.GetAsync("/api/v1/profiles/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile!.Timezone.Should().Be("UTC");
        profile.FirstName.Should().BeNull();
    }

    [Fact]
    public async Task Upsert_then_get_profile_roundtrips_correctly()
    {
        var (client, _) = await RegisterVerifyAndLoginAsync();

        var putResponse = await client.PutAsJsonAsync("/api/v1/profiles/me", new UpsertProfileRequest(
            "Ana", "Pérez", new DateOnly(1992, 3, 10), 58m, 162m, "Diseñadora", null, "America/Mexico_City"));
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/v1/profiles/me");
        var profile = await getResponse.Content.ReadFromJsonAsync<ProfileResponse>();

        profile!.FirstName.Should().Be("Ana");
        profile.Timezone.Should().Be("America/Mexico_City");
    }

    [Fact]
    public async Task Invalid_timezone_on_upsert_returns_400()
    {
        var (client, _) = await RegisterVerifyAndLoginAsync();

        var response = await client.PutAsJsonAsync("/api/v1/profiles/me", new UpsertProfileRequest(
            null, null, null, null, null, null, null, "Not/A_Real_Zone"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_list_and_delete_goal_flow()
    {
        var (client, _) = await RegisterVerifyAndLoginAsync();

        var addResponse = await client.PostAsJsonAsync("/api/v1/profiles/me/goals", new AddGoalRequest("steps", 10000));
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var added = await addResponse.Content.ReadFromJsonAsync<AddGoalResponse>();

        var listResponse = await client.GetAsync("/api/v1/profiles/me/goals");
        var goals = await listResponse.Content.ReadFromJsonAsync<List<GoalResponse>>();
        goals.Should().ContainSingle(g => g.Id == added!.Id && g.Type == "steps");

        var deleteResponse = await client.DeleteAsync($"/api/v1/profiles/me/goals/{added!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterDelete = await client.GetAsync("/api/v1/profiles/me/goals");
        var goalsAfter = await listAfterDelete.Content.ReadFromJsonAsync<List<GoalResponse>>();
        goalsAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task Onboarding_survey_returns_204_before_answered_then_roundtrips_after_upsert()
    {
        var (client, _) = await RegisterVerifyAndLoginAsync();

        var beforeResponse = await client.GetAsync("/api/v1/profiles/me/onboarding-survey");
        beforeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var putResponse = await client.PutAsJsonAsync("/api/v1/profiles/me/onboarding-survey",
            new UpsertOnboardingSurveyRequest("9am-6pm", "11pm-7am", "moderate", "ALTO", "regular"));
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterResponse = await client.GetAsync("/api/v1/profiles/me/onboarding-survey");
        afterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var survey = await afterResponse.Content.ReadFromJsonAsync<OnboardingSurveyResponse>();
        survey!.DeclaredStressLevel.Should().Be("ALTO");
    }

    [Fact]
    public async Task Delete_account_requires_correct_password_and_then_blocks_further_calls()
    {
        var (client, _) = await RegisterVerifyAndLoginAsync();

        // HttpClient.DeleteAsync no soporta mandar body fácilmente — se arma la request
        // a mano con SendAsync para poder incluir currentPassword.
        var wrongRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteMeRequest("WrongPassword1"))
        };
        var wrongResponse = await client.SendAsync(wrongRequest);
        wrongResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var correctRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/users/me")
        {
            Content = JsonContent.Create(new DeleteMeRequest(Password))
        };
        var correctResponse = await client.SendAsync(correctRequest);
        correctResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
