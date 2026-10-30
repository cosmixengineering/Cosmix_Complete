using Cosmix.Api.Authentication;
using Cosmix.Api.Attendance;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Api.Health;
using Cosmix.Api.Hosting;
using Cosmix.Api.Middleware;
using Cosmix.Api.OpenApi;
using Cosmix.Api.Development;
using Cosmix.Application;
using Cosmix.Application.Attendance.Common;
using Cosmix.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

if (args.Length > 0 && string.Equals(args[0], "bootstrap-superadmin", StringComparison.OrdinalIgnoreCase))
{
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST")))
        throw new InvalidOperationException("Fresh local testing cannot invoke production owner bootstrap.");
    Environment.ExitCode = await BootstrapCommand.RunAsync(args[1..]);
    return;
}

var setupLocalTesting = args.Length == 1 && args[0] == "setup-local-testing";
var builder = LocalTesting.CreateBuilder(setupLocalTesting, args);
var localTesting = LocalTesting.Configure(builder);
if (setupLocalTesting)
{
    if (!localTesting) throw new InvalidOperationException("Explicit local testing configuration is required.");
    Environment.ExitCode = await LocalTestingSetup.RunAsync(builder.Configuration);
    return;
}
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
if (localTesting) LocalTesting.Register(builder.Services);
builder.Services.AddApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddHostedService<OtpDeliveryWorker>();
builder.Services.AddHostedService<EmployeeLifecycleWorker>();
builder.Services.AddHostedService<AttendanceOutboxWorker>();
builder.Services.AddHostedService<ZoneRetentionWorker>();
builder.Services.AddHostedService<MissingCheckoutReminderWorker>();
builder.Services.AddSingleton(builder.Configuration.GetSection(
    Cosmix.Application.Attendance.FaceVerification.FaceRetirementWorkerOptions.SectionName)
    .Get<Cosmix.Application.Attendance.FaceVerification.FaceRetirementWorkerOptions>() ?? new());
builder.Services.AddHostedService<FaceRetirementWorker>();
builder.Services.AddHostedService<DocumentExpiryReminderWorker>();
if (localTesting) LocalTesting.ConfigureOwnedHost(builder.Services, LocalTesting.FreshTarget(builder.Configuration));
if (localTesting && !HrReportTestingGuard.IsSelected && !HrPermissionP1Guard.IsSelected && !HrOutstationO1Guard.IsSelected && !HrVehicleV2ContinuationGuard.AnySelected) LocalTesting.ConfigureExistingOwnedHost(builder.Services);
builder.Services.AddAttendanceTrafficControls(builder.Configuration);
builder.Services.AddSingleton<IAttendanceCursorProtector, AttendanceCursorProtector>();
builder.Services.AddSingleton<Cosmix.Application.Attendance.FaceVerification.IFaceCaptureProtector, FaceCaptureProtector>();
Cosmix.Api.Controllers.Attendance.WorkTreatmentProtection.AddWorkTreatmentSourceProtection(builder.Services);
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "Cosmix.Csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
});
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed."
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    };
});
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerDocumentTransformer>();
    options.AddOperationTransformer<BearerOperationTransformer>();
    options.AddOperationTransformer<OrganizationOperationTransformer>();
    options.AddOperationTransformer<DeprecatedOperationTransformer>();
    options.AddSchemaTransformer<AttendancePolicySchemaTransformer>();
});
builder.Services.AddHealthChecks()
    .AddCheck<WorkerReadinessCheck>("background-workers", tags: ["workers"])
    .AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"])
    .AddCheck<PayrollDeploymentReadinessCheck>("payroll-deployment", tags: ["payroll-readiness"]);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Any(origin => origin.Contains('*')))
        throw new InvalidOperationException("CORS requires explicit trusted origins.");
    if (origins.Length > 0)
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

// Later ASP.NET registrations can add Data Protection's hosted service again.
if (localTesting && (HrReportTestingGuard.IsSelected || HrPermissionP1Guard.IsSelected || HrOutstationO1Guard.IsSelected || HrVehicleV2ContinuationGuard.AnySelected)) LocalTesting.ConfigureExistingOwnedHost(builder.Services);
var app = builder.Build();
if (localTesting && HrOutstationO1Guard.IsSelected)
    app.UseMiddleware<CorrelationIdMiddleware>();
HrVehicleTestingBoundary? vehicleTestingBoundary = null;
HrReportTestingBoundary? reportTestingBoundary = null;
HrPermissionP1Boundary? permissionTestingBoundary = null;
HrOutstationO1Boundary? outstationTestingBoundary = null;
HrVehicleV2Boundary? vehicleV2Boundary = null;
if (localTesting)
{
    await LocalTesting.VerifyDatabaseAsync(app.Services);
    if (HrVehicleV2ContinuationGuard.AnySelected)
    {
        vehicleV2Boundary = app.Services.GetRequiredService<HrVehicleV2Boundary>();
        if (HrVehicleV2ContinuationGuard.IsSelected) vehicleV2Boundary.InitializeContinuation();
        app.Use(vehicleV2Boundary.InvokeAsync);
    }
    if (HrOutstationO1Guard.IsSelected)
    {
        outstationTestingBoundary = app.Services.GetRequiredService<HrOutstationO1Boundary>();
        using var fixtureScope = app.Services.CreateScope();
        // Resolve the endpoint graph before validating the retained, immutable test sources.
        _ = fixtureScope.ServiceProvider.GetRequiredService<Cosmix.Application.HR.Allowances.DeploymentService>();
        _ = fixtureScope.ServiceProvider.GetRequiredService<Cosmix.Application.Authentication.Web.WebAuthenticationService>();
        var fixtureDb = fixtureScope.ServiceProvider.GetRequiredService<Cosmix.Infrastructure.Database.CosmixDbContext>();
        await fixtureDb.Database.OpenConnectionAsync();
        await HrOutstationO1Database.VerifyAsync(fixtureDb, builder.Configuration, CancellationToken.None,
            builder.Configuration.GetValue<bool>("LocalTesting:AllowUnencryptedLocalPipe"));
        await HrOutstationO1Fixture.ValidateExistingAsync(fixtureDb, CancellationToken.None);
        app.Use(outstationTestingBoundary.InvokeAsync);
    }
    if (HrPermissionP1Guard.IsSelected)
    {
        permissionTestingBoundary = app.Services.GetRequiredService<HrPermissionP1Boundary>();
        app.Use(permissionTestingBoundary.InvokeAsync);
    }
    if (HrReportTestingGuard.IsSelected)
    {
        reportTestingBoundary = new HrReportTestingBoundary(builder.Configuration);
        app.Use(reportTestingBoundary.InvokeAsync);
    }
    if (HrVehicleTestingGuard.IsSelected)
    {
        vehicleTestingBoundary = new HrVehicleTestingBoundary(builder.Configuration);
        app.Use(vehicleTestingBoundary.InvokeAsync);
    }
    if (HrBusinessTestingBoundary.IsSelected && !HrReportTestingGuard.IsSelected)
    {
        var boundary = new HrBusinessTestingBoundary();
        app.Use(boundary.InvokeAsync);
    }
    if (AuthOnlyTestingBoundary.IsSelected)
    {
        var boundary = new AuthOnlyTestingBoundary();
        app.Use(boundary.InvokeAsync);
    }
    // Even when the host binding is changed, the testing host never serves a remote request.
    app.Use(async (context, next) =>
    {
        if (!LocalTesting.IsLocal(context)) { context.Response.StatusCode = 404; return; }
        await next(context);
    });
    LocalTesting.MapEndpoints(app);
}
if (!localTesting || !HrOutstationO1Guard.IsSelected)
    app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<RequestCancellationMiddleware>();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
    app.UseHsts();
app.UseHttpsRedirection();
app.UseCosmixFrontend();
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    // Serve the local documentation assets before fallback authorization.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("../openapi/v1.json", "Cosmix API v1");
        options.DocumentTitle = "Cosmix API documentation";
    });
}
app.UseAuthentication();
if (vehicleV2Boundary is not null)
    app.Use(vehicleV2Boundary.AuthorizeActorAsync);
if (outstationTestingBoundary is not null)
    app.Use(outstationTestingBoundary.AuthorizeActorAsync);
if (permissionTestingBoundary is not null)
    app.Use(permissionTestingBoundary.AuthorizeActorAsync);
if (reportTestingBoundary is not null)
    app.Use(reportTestingBoundary.AuthorizeActorAsync);
if (vehicleTestingBoundary is not null)
    app.Use(vehicleTestingBoundary.AuthorizeActorAsync);
app.UseRateLimiter();
app.UseMiddleware<WebCsrfMiddleware>();
app.UseAuthorization();
if (vehicleV2Boundary is not null)
    app.Use(vehicleV2Boundary.PrepareOtpAsync);
if (outstationTestingBoundary is not null)
    app.Use(outstationTestingBoundary.PrepareOtpAsync);
if (permissionTestingBoundary is not null)
    app.Use(permissionTestingBoundary.PrepareOtpAsync);
if (reportTestingBoundary is not null)
    app.Use(reportTestingBoundary.PrepareOtpAsync);
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/workers", new HealthCheckOptions { Predicate = check => check.Tags.Contains("workers") }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health/payroll-ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("payroll-readiness"),
    ResultStatusCodes =
    {
        [Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable
    }
}).AllowAnonymous();
app.MapControllers();
app.MapCosmixFrontend();
app.Run();

public partial class Program;
