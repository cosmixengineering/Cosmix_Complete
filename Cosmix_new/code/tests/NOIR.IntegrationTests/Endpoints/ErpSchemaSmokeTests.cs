namespace NOIR.IntegrationTests.Endpoints;

[Collection("Integration")]
public class ErpSchemaSmokeTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ErpSchemaSmokeTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/stock/transfers/?page=1&pageSize=20")]
    [InlineData("/api/stock/indents/?page=1&pageSize=20")]
    [InlineData("/api/invoicing/invoices/")]
    [InlineData("/api/estimation/boqs/")]
    [InlineData("/api/accounting/accounts/")]
    [InlineData("/api/accounting/journal/")]
    public async Task ListEndpoints_OnFreshMigratedDatabase_ShouldReturnOk(string path)
    {
        using var client = _factory.CreateTestClient();
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand("admin@noir.local", "123qwe"));
        login.EnsureSuccessStatusCode();
        var loginResponse = await login.Content.ReadFromJsonAsync<LoginResponse>();
        using var authenticatedClient = _factory.CreateAuthenticatedClient(loginResponse!.Auth!.AccessToken);

        using var response = await authenticatedClient.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
}
