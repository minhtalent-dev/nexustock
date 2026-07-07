using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Nexustock.MasterData.IntegrationTests;

public class AuthControllerTests : IntegrationTestBase
{
    [Fact]
    public async Task Register_And_Login_Should_Succeed()
    {
        // 1. Register a new user
        var tenantId = Guid.NewGuid();
        var registerPayload = new
        {
            email = "testuser@example.com",
            password = "Password123!",
            fullName = "Test User",
            tenantId
        };

        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register", registerPayload);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        // 2. Login with the registered user
        var loginPayload = new
        {
            email = "testuser@example.com",
            password = "Password123!"
        };

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", loginPayload);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);
        Assert.NotNull(authResult!.Token);
        Assert.NotNull(authResult.RefreshToken);
    }

    private record AuthResponse(string Token, string RefreshToken);
}
