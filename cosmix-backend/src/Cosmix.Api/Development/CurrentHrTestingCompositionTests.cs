#if CURRENT_HR_COMPOSITION_TESTS
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using Cosmix.Api.Authentication;
using Cosmix.Api.Attendance;
using Cosmix.Api.BackgroundJobs;
using Cosmix.Api.Health;
using Cosmix.Api.Middleware;
using Cosmix.Api.OpenApi;
using Cosmix.Application;
using Cosmix.Application.Attendance.Common;
using Cosmix.Infrastructure;
using Microsoft.AspNetCore.Mvc;

// Registration only against a frozen API output: never Build, resolve services, start a host or read private settings.
internal static class CurrentHrTestingCompositionTests
{
    public static int Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Supply the frozen API output directory.");
        var resolver = new AssemblyDependencyResolver(Path.Combine(args[0], "Cosmix.Api.dll"));
        AssemblyLoadContext.Default.Resolving += (context, name) => resolver.ResolveAssemblyToPath(name) is { } path
            ? context.LoadFromAssemblyPath(path) : null;
        return Run();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var local = typeof(OtpDeliveryWorker).Assembly.GetType("Cosmix.Api.Development.LocalTesting")!;
        object? Call(string method, params object?[] arguments)
        {
            try { return local.GetMethod(method, flags)!.Invoke(null, arguments); }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        var previous = new[] { "COSMIX_EXISTING_TESTING_HOST", "COSMIX_FRESH_TESTING_HOST" }
            .ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("COSMIX_EXISTING_TESTING_HOST", "HumaHrAcceptance20261019C01");
            Environment.SetEnvironmentVariable("COSMIX_FRESH_TESTING_HOST", null);
            var builder = (WebApplicationBuilder)Call("CreateBuilder", false, Array.Empty<string>())!;
            // Registration-only scalar values; no connection or private storage is ever resolved.
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CosmixDatabase"] = "Server=offline.invalid;Database=Forbidden;Integrated Security=true",
                ["MobileAuthentication:Enabled"] = "true", ["WebAuthentication:Enabled"] = "true",
                ["MobileAuthentication:OtpLifetimeSeconds"] = "300", ["MobileAuthentication:ResendCooldownSeconds"] = "30",
                ["MobileAuthentication:MaximumOtpAttempts"] = "5", ["MobileAuthentication:RateWindowSeconds"] = "300",
                ["MobileAuthentication:RequestsPerPhone"] = "10", ["MobileAuthentication:RequestsPerIp"] = "30",
                ["MobileAuthentication:RequestsGlobal"] = "100", ["MobileAuthentication:VerificationAttemptsPerPhone"] = "10",
                ["MobileAuthentication:VerificationAttemptsPerIp"] = "30", ["MobileAuthentication:VerificationAttemptsGlobal"] = "100",
                ["MobileAuthentication:AccessTokenLifetimeSeconds"] = "300", ["MobileAuthentication:RefreshTokenLifetimeSeconds"] = "3600",
                ["MobileAuthentication:SessionLifetimeSeconds"] = "7200", ["MobileAuthentication:MinimumRequestDurationMilliseconds"] = "100",
                ["MobileAuthentication:BusinessTimeZoneId"] = "Asia/Karachi", ["MobileAuthentication:Sms:Provider"] = "LocalTesting",
                ["MobileAuthentication:Crypto:MaxDeliveryLifetimeSeconds"] = "300",
                ["WebAuthentication:MinimumPasswordLength"] = "12", ["WebAuthentication:MaximumPasswordFailures"] = "5",
                ["WebAuthentication:PasswordLockoutSeconds"] = "300", ["WebAuthentication:PasswordAttemptsPerAccount"] = "10",
                ["WebAuthentication:PasswordAttemptsPerEmail"] = "10", ["WebAuthentication:PasswordAttemptsPerIp"] = "30",
                ["WebAuthentication:PasswordAttemptsGlobal"] = "100", ["Urls"] = "https://localhost:7481",
                ["Jwt:Issuer"] = "offline", ["Jwt:Audience"] = "offline", ["Jwt:SigningKey"] = new string('x', 64)
            });
            var services = builder.Services;
            services.AddApplication();
            services.AddInfrastructure(builder.Configuration);
            Call("Register", services);
            services.AddApiAuthentication(builder.Configuration, builder.Environment);
            services.AddHostedService<OtpDeliveryWorker>();
            services.AddHostedService<EmployeeLifecycleWorker>();
            services.AddHostedService<AttendanceOutboxWorker>();
            services.AddHostedService<ZoneRetentionWorker>();
            services.AddHostedService<MissingCheckoutReminderWorker>();
            services.AddSingleton(builder.Configuration.GetSection(
                Cosmix.Application.Attendance.FaceVerification.FaceRetirementWorkerOptions.SectionName)
                .Get<Cosmix.Application.Attendance.FaceVerification.FaceRetirementWorkerOptions>() ?? new());
            services.AddHostedService<FaceRetirementWorker>();
            services.AddHostedService<DocumentExpiryReminderWorker>();
            PrintWorkers("before C01", services);
            var failed = false;
            try
            {
#if CURRENT_HR_COMPOSITION_PATCHED
                Call("RemoveUnboundedWorkers", services);
                Cosmix.Api.Development.CurrentHrTestingHost.ConfigureServices(services);
#else
                Call("ConfigureExistingOwnedHost", services);
#endif
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("RED: complete API registrations rejected by C01 before host construction.");
                failed = true;
            }
            PrintWorkers("after C01", services);
#if CURRENT_HR_COMPOSITION_PATCHED
            var expectedDataProtection = typeof(DataProtectionServiceCollectionExtensions).Assembly.GetType(
                "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService", true)!;
            var guardedWorkers = services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
            if (guardedWorkers.Length != 2 || guardedWorkers.Count(s => s.ImplementationType == typeof(OtpDeliveryWorker)) != 1 ||
                guardedWorkers.Count(s => s.ImplementationType == expectedDataProtection) != 1)
                throw new InvalidOperationException("Unexpected services at the guarded boundary.");
            var rejected = 0;
            void Reject(ServiceDescriptor descriptor)
            {
                var probe = new ServiceCollection();
                foreach (var worker in guardedWorkers) ((IServiceCollection)probe).Add(worker);
                ((IServiceCollection)probe).Add(descriptor);
                try { Cosmix.Api.Development.CurrentHrTestingHost.ConfigureServices(probe); }
                catch (InvalidOperationException) { rejected++; return; }
                throw new InvalidOperationException("Unexpected worker was accepted.");
            }
            Reject(ServiceDescriptor.Singleton<IHostedService, UnexpectedWorker>());
            Reject(ServiceDescriptor.Singleton<IHostedService>(_ => throw new Exception("Factory must never be invoked.")));
            Reject(ServiceDescriptor.Singleton<IHostedService>(new UnexpectedWorker()));
            Reject(ServiceDescriptor.Transient(typeof(IHostedService), expectedDataProtection));
            foreach (var businessWorker in new[] { typeof(EmployeeLifecycleWorker), typeof(AttendanceOutboxWorker), typeof(ZoneRetentionWorker),
                typeof(MissingCheckoutReminderWorker), typeof(FaceRetirementWorker), typeof(DocumentExpiryReminderWorker) })
                Reject(ServiceDescriptor.Singleton(typeof(IHostedService), businessWorker));
            var spoof = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("UntrustedHostingLookalike"), AssemblyBuilderAccess.Run)
                .DefineDynamicModule("lookalike").DefineType(expectedDataProtection.FullName!, TypeAttributes.Public | TypeAttributes.Abstract,
                    typeof(BackgroundService)).CreateType()!;
            Reject(ServiceDescriptor.Singleton(typeof(IHostedService), spoof));
            Console.WriteLine($"PASS: {rejected} unexpected, business, factory, instance, lifetime and framework-lookalike registrations rejected.");
#endif

            services.AddAttendanceTrafficControls(builder.Configuration);
            services.AddSingleton<IAttendanceCursorProtector, AttendanceCursorProtector>();
            services.AddSingleton<Cosmix.Application.Attendance.FaceVerification.IFaceCaptureProtector, FaceCaptureProtector>();
            Cosmix.Api.Controllers.Attendance.WorkTreatmentProtection.AddWorkTreatmentSourceProtection(services);
            services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
                options.Cookie.Name = "__Host-Cosmix.Csrf";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Path = "/";
            });
            services.AddControllers().ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var problem = new ValidationProblemDetails(context.ModelState)
                    { Status = StatusCodes.Status400BadRequest, Title = "Request validation failed." };
                    problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                    var result = new BadRequestObjectResult(problem);
                    result.ContentTypes.Add("application/problem+json");
                    return result;
                };
            });
            services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
            services.AddExceptionHandler<ApiExceptionHandler>();
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer<BearerDocumentTransformer>();
                options.AddOperationTransformer<BearerOperationTransformer>();
                options.AddOperationTransformer<OrganizationOperationTransformer>();
                options.AddOperationTransformer<DeprecatedOperationTransformer>();
                options.AddSchemaTransformer<AttendancePolicySchemaTransformer>();
            });
            services.AddHealthChecks().AddCheck<DatabaseReadinessCheck>("database", tags: ["ready"]);
            services.AddCors(options => options.AddDefaultPolicy(policy =>
            {
                var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (origins.Any(origin => origin.Contains('*'))) throw new InvalidOperationException("Explicit origins required.");
                if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }));
            PrintWorkers("complete pre-Build API", services);
            if (failed) return 1;
#if CURRENT_HR_COMPOSITION_PATCHED
            var publisher = typeof(HealthCheckServiceCollectionExtensions).Assembly.GetType(
                "Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckPublisherHostedService", true)!;
            var completeWorkers = services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
            if (completeWorkers.Length != 3 || completeWorkers.Any(s => s.ImplementationType != typeof(OtpDeliveryWorker) &&
                    s.ImplementationType != expectedDataProtection && s.ImplementationType != publisher) ||
                services.Any(s => s.ServiceType == typeof(Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheckPublisher)))
                throw new InvalidOperationException("Full API composition added an unexpected worker or health publisher.");
#endif
            if (services.Any(s => s.ServiceType == typeof(IHostedService) && s.ImplementationType?.Assembly == typeof(OtpDeliveryWorker).Assembly &&
                s.ImplementationType != typeof(OtpDeliveryWorker)) || services.Any(s => s.ServiceType == typeof(IHostedService) &&
                s.ImplementationType?.Assembly == typeof(Cosmix.Infrastructure.DependencyInjection).Assembly))
                throw new InvalidOperationException("A business worker survived full composition.");
            Console.WriteLine("PASS: complete API service registration accepts C01 and retains no business worker except local OTP. No host, provider resolution, SQL or private IO.");
            return 0;
        }
        finally { foreach (var (key, value) in previous) Environment.SetEnvironmentVariable(key, value); }
    }

    private static void PrintWorkers(string stage, IServiceCollection services)
    {
        foreach (var descriptor in services.Where(s => s.ServiceType == typeof(IHostedService)))
            Console.WriteLine($"{stage}: lifetime={descriptor.Lifetime}; type={descriptor.ImplementationType?.AssemblyQualifiedName ?? "<none>"}; " +
                $"factory={descriptor.ImplementationFactory?.Method.ToString() ?? "<none>"}; instance={descriptor.ImplementationInstance?.GetType().AssemblyQualifiedName ?? "<none>"}");
    }

    private sealed class UnexpectedWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken ct) => throw new Exception("Workers must never start in registration tests.");
    }
}
#endif
