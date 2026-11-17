using System.Net;

namespace CryptoFxTracker.IntegrationTests.Api;

public sealed class HealthEndpointTests : IDisposable
{
    private readonly ApiFactory factory = new();

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Health_IsAnonymous_AndHealthyWhenDatabaseIsReachable()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
