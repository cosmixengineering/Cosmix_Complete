#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using Cosmix.Api.Attendance;
using Cosmix.Api.Authentication;
using System.Security.Cryptography;
using Cosmix.Application;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.FaceVerification;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Punches.CaptureValidation;
using Cosmix.Application.Attendance.Zones;
using Cosmix.Application.Attendance.MonthLocks;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Overtime;
using Cosmix.Application.Attendance.Waiting;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Application.HR.Reports;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
foreach (var environment in new[] { Environments.Development, Environments.Production })
{
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:CosmixDatabase"] = "Server=tcp:127.0.0.1,1;Database=Cosmix_NoConnection;Integrated Security=true;Encrypt=true;Connect Timeout=1",
        ["MobileAuthentication:Enabled"] = "false", ["WebAuthentication:Enabled"] = "false",
        ["Jwt:Issuer"] = "Cosmix.Composition.Check", ["Jwt:Audience"] = "Cosmix.Composition.Check",
        ["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        ["SelfHostedFace:Enabled"] = "true", ["FaceTemplateStorage:Enabled"] = "false"
    }).Build();
    var services = new ServiceCollection();
    services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
    var hostEnvironment=new EnvironmentStub(environment);
    services.AddSingleton<IHostEnvironment>(hostEnvironment);
    services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
    services.AddSingleton<IAttendanceCursorProtector, AttendanceCursorProtector>();
    services.AddSingleton<IFaceCaptureProtector, FaceCaptureProtector>();
    services.AddApplication(); services.AddInfrastructure(configuration);
    Cosmix.Api.Controllers.Attendance.WorkTreatmentProtection.AddWorkTreatmentSourceProtection(services);
    services.AddApiAuthentication(configuration,hostEnvironment);
    // Only storage IO is replaced. No private paths/keys are provisioned or real SQL opened.
    services.Replace(ServiceDescriptor.Singleton<IPrivateFileStore, NoFiles>());
    using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    using var scope = provider.CreateScope();
    var scoped = scope.ServiceProvider;
    Check(scoped.GetRequiredService<LeaveEncashmentService>() is not null &&
        scoped.GetRequiredService<LeaveEncashmentValuation>() is not null,
        "Encashment resolves owning leave, KPI, compensation and evidence services without duplicate modules");
    Check(scoped.GetRequiredService<LeaveTeamCalendarService>() is not null,
        "Scoped team calendar resolves the existing leave, roster and authorization repositories");
    Check(scoped.GetRequiredService<LeaveBenefitService>() is not null &&
        scoped.GetRequiredService<LeaveBenefitKpiReader>() is not null &&
        scoped.GetRequiredService<ILeaveBenefitCreditReader>() is LeaveBenefitEvidence &&
        scoped.GetRequiredService<ILeaveBenefitRepository>() is Cosmix.Infrastructure.Repositories.HR.LeaveBenefitRepository &&
        Cosmix.Application.Authentication.Access.AccessPermissionCatalog.Find(LeaveBenefitPermissions.Review) is not null,
        "Benefit award, verified quota reader, owning repository and explicit review permission resolve");
    Check(scoped.GetRequiredService<LeaveRecoveryWorkService>() is not null &&
        scoped.GetRequiredService<ILeaveRecoveryCreditReader>() is LeaveRecoveryCreditReader &&
        scoped.GetRequiredService<ILeaveRecoveryWorkRepository>() is Cosmix.Infrastructure.Repositories.HR.LeaveRecoveryWorkRepository,
        "Recovery actual-work service and exclusive-credit reader resolve without circular dependencies");
    Check(scoped.GetRequiredService<Cosmix.Application.HR.VehicleFuel.VehicleFuelService>() is not null &&
        Cosmix.Application.Authentication.Access.AccessPermissionCatalog.Find(
            Cosmix.Application.HR.VehicleFuel.VehiclePolicyPermissions.Manage) is not null,
        "Vehicle policy publication uses the existing module and registered dedicated permission");
    Check(scoped.GetRequiredService<HrReportService>() is not null &&
        scoped.GetRequiredService<HrReportAuthorization>() is not null &&
        scoped.GetRequiredService<IHrReportRepository>() is Cosmix.Infrastructure.Repositories.HR.HrReportRepository,
        "HR report authorization and scoped repository resolve with the real registration graph");
    Check(scoped.GetRequiredService<IAdvancedAttendancePolicyReader>() is AdvancedPolicyReader,
        "actual typed advanced policy source is registered");
    Check(scoped.GetRequiredService<AttendanceKpiService>() is not null &&
        scoped.GetRequiredService<IKpiSourceReader>() is KpiSourceReader &&
        scoped.GetRequiredService<IFinalizedKpiReader>() is FinalizedKpiReader,
        "actual KPI annual calculation and retained finalization graph resolves without recursion");
    Check(scoped.GetRequiredService<KpiDisputeService>() is not null &&
        scoped.GetRequiredService<KpiMissingEvidenceService>() is not null,
        "actual KPI dispute and missing-evidence revision services resolve");
    Check(scoped.GetRequiredService<WaitingWorkflowService>() is not null &&
        scoped.GetRequiredService<IWaitingPerformanceEffectReader>() is WaitingPerformanceEffectReader,
        "actual independent waiting review and KPI effect reader are registered");
    Check(scoped.GetRequiredService<WorkTreatmentService>() is not null &&
        scoped.GetRequiredService<IWorkTreatmentCreditReader>() is WorkTreatmentCreditReader &&
        scoped.GetRequiredService<IWorkTreatmentProposalProtector>() is WorkTreatmentTicketProtector,
        "actual work-treatment graph uses the API source-ticket protector");
    Check(scoped.GetRequiredService<IAttendanceReviewedWorkReader>() is AttendanceReviewedWorkReader &&
        scoped.GetRequiredService<OvertimeService>() is not null,
        "ordinary-time and OT workflows resolve their physical work-allocation reader");
    Check(ReferenceEquals(scoped.GetRequiredService<IMonthLockOvertimeReader>(),scoped.GetRequiredService<OvertimeService>()),
        "month lock uses owning OT service for protected award validation");
    Check(ReferenceEquals(scoped.GetRequiredService<IMonthLockSnapshotReader>(),scoped.GetRequiredService<MonthLockService>()) &&
        scoped.GetRequiredService<Cosmix.Application.HR.PayrollHandoff.PayrollHandoffService>() is not null,
        "Finance resolves the existing month-lock reader without a duplicate module or dependency cycle");
    Check(scoped.GetRequiredService<IMonthLockFinancialReader>() is AttendanceMonthLockFinancialReader,
        "actual monthly dependency reader resolves without a circular DI graph");
    Check(scoped.GetRequiredService<IAttendancePayrollReconciliationSourceReader>() is AttendancePayrollReconciliationSourceReader,
        "actual receipt source adapter is registered");
    Check(scoped.GetRequiredService<IAttendancePayrollInputProducer>() is AttendancePayrollInputProducer,
        "actual financial producer can resolve its receipt reader");
    Check(scoped.GetRequiredService<MonthLockService>() is not null &&
        scoped.GetRequiredService<FaceCaptureService>() is not null, "existing services resolve new dependencies");
    Check(scoped.GetRequiredService<OfficeBoundaryService>() is not null &&
        scoped.GetRequiredService<IOfficeAttendanceSourceReader>() is OfficeAttendanceSourceReader,
        "real office boundary/source services use actual registered dependencies");
    Check(scoped.GetRequiredService<ICaptureValidationEvaluator>() is CaptureValidationEvaluator &&
        scoped.GetRequiredService<ICaptureValidationPolicyReader>() is CaptureValidationPolicyService,
        "required capture policy/evaluator graph is registered, not an optional fallback");
    Check(scoped.GetRequiredService<IAttendancePayrollJurisdictionReader>() is OfficeAttendancePayrollJurisdictionReader,
        "office jurisdiction decorator resolves without recursive interface DI");
    Check(scoped.GetRequiredService<CorrectionReviewService>() is not null &&
        ReferenceEquals(scoped.GetRequiredService<ICorrectionAlternativeEvidenceReader>(),scoped.GetRequiredService<CorrectionAlternativePolicyService>()),
        "special Admin evidence reader shares its scoped service and existing corrections graph");
    Check(scoped.GetRequiredService<FaceRetirementProcessor>() is not null &&
        scoped.GetRequiredService<FaceDisposalRunner>() is not null,
        "durable face retirement graph resolves without enabling physical disposal");
    Check(scoped.GetRequiredService<ZoneOfflineObservationService>() is not null &&
        !scoped.GetRequiredService<IZoneClaimStore>().IsConfigured,
        "offline zone workflow resolves but unconfigured private claim storage remains unavailable");
    var store = scoped.GetRequiredService<IFaceTemplateStore>();
    Check(!store.IsConfigured, "missing private retention/storage configuration never permits biometric use");
    Check(store is RetentionControlledFaceTemplateStore,
        "selected self-hosted provider always receives the use gate in every environment");
    try
    {
        var face = scoped.GetRequiredService<IFaceVerificationProvider>();
        Check(false, "selected adapter with missing endpoint configuration must reject construction");
    }
    catch(AuthenticationFailure failure) when(failure.StatusCode==503 && failure.Code=="face_provider_not_configured")
    {
        Check(failure.Code == "face_provider_not_configured", "selected but unconfigured transport fails explicitly with 503 in every environment");
    }
}
Console.WriteLine($"PASS: {checks} actual Attendance composition checks; synthetic storage, no SQL/network/private-key writes.");

sealed class NoFiles : IPrivateFileStore
{
    public Task<PrivateFileReference> WriteAsync(byte[] content, string purpose, CancellationToken ct) => throw new Exception("No IO permitted.");
    public Task<byte[]> ReadAsync(PrivateFileReference reference, string purpose, CancellationToken ct) => throw new Exception("No IO permitted.");
}
sealed class EnvironmentStub(string environment) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environment;
    public string ApplicationName { get; set; } = "Cosmix.Api";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
