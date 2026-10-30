using Microsoft.Extensions.FileProviders;

namespace Cosmix.Api.Hosting;

/// <summary>Opt-in same-origin React hosting. API and health misses never become successful HTML responses.</summary>
public static class IisFrontendHosting
{
    public static void UseCosmixFrontend(this WebApplication app)
    {
        if (!Enabled(app)) return;
        var files = new PhysicalFileProvider(Root(app));
        app.Lifetime.ApplicationStopped.Register(files.Dispose);
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
                if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    context.Context.Response.Headers.CacheControl = "no-store";
            }
        });
    }

    public static void MapCosmixFrontend(this WebApplication app)
    {
        if (!Enabled(app)) return;
        var index = Path.Combine(Root(app), "index.html");
        app.MapFallback("{*path:nonfile}", async context =>
        {
            if ((!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) ||
                new[] { "/api", "/health", "/swagger", "/openapi", "/assets" }
                    .Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)) ||
                !(context.Request.GetTypedHeaders().Accept?.Any(x => x.MediaType.Value == "text/html") ?? false))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (!HttpMethods.IsHead(context.Request.Method)) await context.Response.SendFileAsync(index, context.RequestAborted);
        }).AllowAnonymous();
    }

    private static bool Enabled(WebApplication app) => app.Configuration.GetValue<bool>("WebFrontend:Enabled");
    private static string Root(WebApplication app)
    {
        var root = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        if (!File.Exists(Path.Combine(root, "index.html")))
            throw new InvalidOperationException("WebFrontend is enabled but the published React index is missing.");
        return root;
    }
}
