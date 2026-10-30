using System.Net;
using Cosmix.Api.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cosmix.Tests;

public sealed class IisFrontendHostingTests
{
    [Fact]
    public async Task Spa_deep_links_work_but_api_health_and_missing_assets_never_return_index()
    {
        var root = Path.Combine(Path.GetTempPath(), "cosmix-iis-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "wwwroot", "assets"));
        await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "index.html"), "<html>synthetic SPA</html>");
        await File.WriteAllTextAsync(Path.Combine(root, "wwwroot", "assets", "test.js"), "/* synthetic */");
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = "wwwroot" });
            builder.WebHost.UseTestServer();
            builder.Services.AddRouting();
            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["WebFrontend:Enabled"] = "true" });
            await using var app = builder.Build();
            app.UseCosmixFrontend();
            app.MapGet("/api/v1/ping", () => Results.Json(new { ok = true }));
            app.MapGet("/api/v1/protected", () => Results.NoContent()).RequireAuthorization();
            app.MapCosmixFrontend();
            await app.StartAsync();
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            foreach (var path in new[] { "/", "/sign-in", "/hr/payroll/handoff", "/account" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("synthetic SPA", await response.Content.ReadAsStringAsync());
                Assert.True(response.Headers.CacheControl?.NoStore);
            }
            foreach (var path in new[] { "/api/v1/missing", "/API/missing", "/health/missing", "/assets/missing.js", "/swagger/missing", "/openapi/missing" })
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
            using var post = await client.PostAsync("/hr/payroll", null);
            Assert.False(post.IsSuccessStatusCode);
            using var api = await client.GetAsync("/api/v1/ping");
            Assert.Equal("application/json", api.Content.Headers.ContentType?.MediaType);
            using var protectedApi = await client.GetAsync("/api/v1/protected");
            Assert.Equal(HttpStatusCode.Unauthorized, protectedApi.StatusCode);
            Assert.DoesNotContain("synthetic SPA", await protectedApi.Content.ReadAsStringAsync());
            using var asset = await client.GetAsync("/assets/test.js");
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
