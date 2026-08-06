using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Unidemix.Api.Contracts;

namespace Unidemix.Api.Tests;

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_is_available()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_requires_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/courses");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Demo_user_can_login_and_read_seeded_course()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("demo@unidemix.local", "Demo123!"));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await client.GetAsync("/api/courses");
        response.EnsureSuccessStatusCode();
        Assert.Contains("german-for-real-life", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registration_rejects_duplicate_email()
    {
        var client = factory.CreateClient();
        var request = new RegisterRequest("new@unidemix.local", "Password123!", "New User");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", request)).StatusCode);
    }
}
