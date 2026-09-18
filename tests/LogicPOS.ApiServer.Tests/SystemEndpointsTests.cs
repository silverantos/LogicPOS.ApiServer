using Microsoft.AspNetCore.Mvc.Testing;

namespace LogicPOS.ApiServer.Tests;

public class SystemEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SystemEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ApiVersion_ReturnsExpectedPlainText()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/system/api-version");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("1.5.2 retail", body);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }
}
