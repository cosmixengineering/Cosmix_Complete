#:project ../../src/Cosmix.Api/Cosmix.Api.csproj
#:property PublishAot=false
#:property RestorePackagesWithLockFile=false
using System.Reflection;
using Cosmix.Api.Attendance;
using Cosmix.Api.Controllers.Attendance;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.Kpi;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Attendance.WorkTreatments;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Notifications;
using Cosmix.Application.HR.Policies.Advanced;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAttendanceKpiApplication();
services.AddWorkTreatments();
services.AddWorkTreatmentSourceProtection();
services.AddSingleton<TimeProvider>(TimeProvider.System);
services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
services.AddScoped<AttendanceOperation>();
services.AddScoped<AttendanceSummaryService>();
void Leaf<T>() where T : class => services.AddScoped<T>(_ => DispatchProxy.Create<T, NoIoProxy>());
Leaf<ICurrentAuthentication>(); Leaf<IAccountAccessReader>(); Leaf<IAccessManagementRepository>();
Leaf<ISchedulingJournalRepository>(); Leaf<IAuthenticationRepository>(); Leaf<IPrivateFileStore>();
Leaf<IWorkTreatmentRepository>(); Leaf<IKpiRepository>(); Leaf<IIndividualRosterResolver>();
Leaf<IAdvancedAttendancePolicyReader>(); Leaf<IAttendanceSummaryRepository>(); Leaf<INotificationOutbox>();
// This constructor probe models the future calculator-to-credit dependency. It does not calculate time.
services.AddScoped<IAttendancePolicyCalculator, CreditConstructorProbe>();
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
using var scope = provider.CreateScope();
var credits = scope.ServiceProvider.GetRequiredService<IWorkTreatmentCreditReader>();
if (credits is not WorkTreatmentCreditReader) throw new Exception("Actual work credit reader registration missing");
if (scope.ServiceProvider.GetRequiredService<IWorkTreatmentProposalProtector>() is not WorkTreatmentTicketProtector)
    throw new Exception("Actual API source ticket protector registration missing");
var kpi = scope.ServiceProvider.GetRequiredService<IKpiSourceReader>();
if (kpi is not KpiSourceReader) throw new Exception("Actual KPI reader registration missing");
var consumer = (CreditConstructorProbe)scope.ServiceProvider.GetRequiredService<IAttendancePolicyCalculator>();
if (!ReferenceEquals(consumer.Credits, credits)) throw new Exception("Scoped credit consumer identity changed");
var annual = scope.ServiceProvider.GetRequiredService<IWorkTreatmentAnnualLateReader>();
if (annual is not KpiAnnualLateReader || typeof(KpiAnnualLateReader).GetConstructors().Single().GetParameters().Any(x => x.ParameterType == typeof(IKpiSourceReader)))
    throw new Exception("Annual reader eagerly binds the KPI reader and recreates the consumer cycle");
if (NoIoProxy.Calls != 0) throw new Exception("Composition check invoked business/auth/provider IO");
Console.WriteLine("PASS: 6 actual registration/API-protector/scoped-reader/lazy-annual constructor composition checks. Calculator probe only; no ordinary calculation, host, SQL or provider acceptance.");

class NoIoProxy : DispatchProxy {
    public static int Calls;
    protected override object? Invoke(MethodInfo? method, object?[]? args) { Calls++; throw new Exception("Unexpected IO in DI-only check"); }
}
sealed class CreditConstructorProbe(IWorkTreatmentCreditReader credits) : IAttendancePolicyCalculator {
    public IWorkTreatmentCreditReader Credits => credits;
    public Task<AttendancePolicyResult> CalculateAsync(Guid employeeId, DateOnly date, FactualAttendance facts, EffectiveRosterResponse roster, CancellationToken ct) =>
        throw new Exception("Constructor probe must not fabricate a calculation result");
}
