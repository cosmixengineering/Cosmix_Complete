using Cosmix.Application.Common.Interfaces;
using Cosmix.Application.HR.Departments;
using Cosmix.Application.HR.Organization;
using Cosmix.Application.Attendance.Sites;
using Cosmix.Application.Attendance.Offices;
using Cosmix.Application.HR.Employees;
using Cosmix.Application.HR.Compensation;
using Cosmix.Application.HR.Policies;
using Cosmix.Application.HR.Leaves;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Application.HR.Dashboard;
using Cosmix.Application.Attendance.Calculation;
using Cosmix.Application.Common.Files;
using Cosmix.Infrastructure.Files;
using Cosmix.Infrastructure.Zones;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Cosmix.Infrastructure.Notifications;
using Cosmix.Application.Attendance.DailySummaries;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Attendance.LegalReview;
using Cosmix.Application.HR.CompanySettings;
using Cosmix.Application.HR.StaffCategories;
using Cosmix.Application.HR.FoundationPolicies;
using Cosmix.Application.Attendance.Corrections;
using Cosmix.Application.Attendance.Audit;
using Cosmix.Application.Attendance.Reports;
using Cosmix.Infrastructure.Database;
using Cosmix.Application.Authentication;
using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Application.Attendance.PunchRetention;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.StatutoryContributions;
using Cosmix.Infrastructure.Workers;
using Cosmix.Infrastructure.Authentication;
using Cosmix.Infrastructure.Authentication.Providers;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Application.Authentication.Web;
using Cosmix.Infrastructure.Authentication.Web;
using Cosmix.Domain.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Cosmix.Application.Common.Approvals;
using Cosmix.Infrastructure.Repositories.Approvals;

namespace Cosmix.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CosmixDbContext>(options =>
        {
            var connection = configuration.GetConnectionString("CosmixDatabase");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Configure ConnectionStrings:CosmixDatabase using user secrets or environment variables.");

            options.UseSqlServer(connection, sql =>
            {
                sql.MigrationsAssembly(typeof(CosmixDbContext).Assembly.FullName);
                sql.CommandTimeout(30);
            });
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CosmixDbContext>());
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<ISiteRepository, SiteRepository>();
        services.AddScoped<ISiteGeofenceRepository, SiteGeofenceRepository>();
        services.AddScoped<ISiteOperationalRepository, SiteOperationalRepository>();
        services.AddScoped<IOfficeBoundaryRepository, OfficeBoundaryRepository>();
        services.AddSchedulingInfrastructure();
        services.AddScoped<Cosmix.Application.Attendance.Shifts.ISchedulingNotificationRepository, SchedulingNotificationRepository>();
        services.AddSiteReportsInfrastructure();
        services.AddAttendanceCaptureInfrastructure();
        services.AddCaptureValidationInfrastructure();
        services.AddAttendanceCorrectionsInfrastructure();
        services.AddScoped<IAttendanceExceptionRepository, AttendanceExceptionRepository>();
        services.AddScoped<IAttendanceAuditRepository, AttendanceAuditRepository>();
        services.AddMonthLocksInfrastructure();
        services.AddAttendanceCarryForwardInfrastructure();
        services.AddAttendanceOvertimeInfrastructure();
        services.AddAllowancesInfrastructure();
        services.AddScoped<PayrollHandoffRepository>();
        services.AddScoped<IPayrollHandoffRepository>(provider => provider.GetRequiredService<PayrollHandoffRepository>());
        services.AddScoped<Cosmix.Application.HR.Allowances.IAllowanceSettlementStore>(provider =>
            provider.GetRequiredService<PayrollHandoffRepository>());
        services.AddAdvancesLoansInfrastructure();
        services.AddVehicleFuelInfrastructure();
        services.AddAttendanceZonesInfrastructure(configuration);
        services.AddScoped<IAttendanceSummaryRepository, AttendanceSummaryRepository>();
        services.AddScoped<IAttendancePayrollInputRepository, AttendancePayrollInputRepository>();
        services.AddScoped<IAttendancePayrollInputLedger, AttendancePayrollInputLedger>();
        services.AddScoped<IAttendancePayrollAttestationRepository, AttendancePayrollAttestationRepository>();
        services.AddScoped<IAttendancePayrollFinancialRepository, AttendancePayrollFinancialRepository>();
        services.AddScoped<Cosmix.Application.HR.Allowances.IDeploymentRepository, DeploymentRepository>();
        services.AddScoped<IAttendanceLegalReviewRepository, AttendanceLegalReviewRepository>();
        services.AddSingleton<IAttendanceLegalReviewPdfInspector, AttendanceLegalReviewPdfInspector>();
        services.AddScoped<ICompanySettingsRepository, CompanySettingsRepository>();
        services.AddScoped<IAttendancePolicyRepository, AttendancePolicyRepository>();
        services.AddAdvancedAttendancePoliciesInfrastructure();
        services.AddAttendanceKpiRepository();
        services.AddKpiMissingEvidenceInfrastructure();
        services.AddAttendanceWaitingInfrastructure();
        services.AddWorkTreatments();
        services.AddScoped<ILeaveRepository, LeaveRepository>();
        services.AddScoped<ILeaveEncashmentRepository, LeaveEncashmentRepository>();
        services.AddScoped<ILeaveBenefitRepository, LeaveBenefitRepository>();
        services.AddScoped<ILeaveRecoveryWorkRepository, LeaveRecoveryWorkRepository>();
        services.AddScoped<IAttendanceCalculationRepository, AttendanceCalculationRepository>();
        services.AddScoped<IAttendanceReportRepository, AttendanceReportRepository>();
        services.AddHrReportsInfrastructure();
        services.AddScoped<IEmployeeSelfServiceRepository, EmployeeSelfServiceRepository>();
        services.AddScoped<IEmployeePayrollHistoryReader, EmployeeSelfServiceRepository>();
        services.AddAttendanceNotificationsInfrastructure(configuration);
        services.AddMissingCheckoutReminders(configuration);
        services.AddScoped<IEmployeeHiringRepository, EmployeeHiringRepository>();
        services.AddScoped<IEmployeeApprovalRepository, EmployeeApprovalRepository>();
        services.AddScoped<IEmployeeSiteAssignmentRepository, EmployeeSiteAssignmentRepository>();
        services.AddScoped<IEmployeeLifecycleRepository, EmployeeLifecycleRepository>();
        services.AddScoped<IEmployeeCompensationRepository, EmployeeCompensationRepository>();
        services.AddScoped<IHrDashboardPopulationRepository, HrDashboardPopulationRepository>();
        services.AddScoped<IHrDashboardPayrollRepository, HrDashboardPayrollRepository>();
        services.AddScoped<IEmployeeUpdateRepository, EmployeeUpdateRepository>();
        services.AddScoped<IApprovalRoutingRepository, ApprovalRoutingRepository>();
        services.AddScoped<IEmployeeBankRepository, EmployeeBankRepository>();
        services.AddScoped<IEmployeeDocumentRepository, EmployeeDocumentRepository>();
        services.AddSingleton(configuration.GetSection(EmployeeFieldPolicyOptions.SectionName).Get<EmployeeFieldPolicyOptions>() ?? new());
        services.AddSingleton(configuration.GetSection(EmployeeLifecycleOptions.SectionName).Get<EmployeeLifecycleOptions>() ?? new());
        services.AddSingleton(configuration.GetSection(EmployeeRecordPolicyOptions.SectionName).Get<EmployeeRecordPolicyOptions>() ?? new());
        services.AddScoped<IEmployeeRecordRepository, EmployeeRecordRepository>();
        services.AddScoped<IStaffTaxonomyRepository, StaffTaxonomyRepository>();
        services.AddScoped<IHrFoundationPolicyRepository, FoundationPolicyRepository>();
        services.AddSingleton(configuration.GetSection(StaffTaxonomyAuthorityOptions.SectionName).Get<StaffTaxonomyAuthorityOptions>() ?? new());
        services.AddSingleton(configuration.GetSection(HrFoundationPolicyAuthorityOptions.SectionName).Get<HrFoundationPolicyAuthorityOptions>() ?? new());
        services.AddSingleton(configuration.GetSection(PrivateFileStorageOptions.SectionName).Get<PrivateFileStorageOptions>() ?? new());
        services.AddSingleton(configuration.GetSection(ClamAvScannerOptions.SectionName).Get<ClamAvScannerOptions>() ?? new());
        services.AddSingleton<IPrivateFileStore, EncryptedPrivateFileStore>();
        services.AddScoped(provider => new Lazy<IPrivateFileStore>(() => provider.GetRequiredService<IPrivateFileStore>()));
        services.AddSingleton<IEmployeeFileScanner, ClamAvEmployeeFileScanner>();
        services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
        services.AddScoped<IPhoneRecoveryRepository, PhoneRecoveryRepository>();
        services.AddScoped<IOtpDeliveryRepository, OtpDeliveryRepository>();
        services.AddScoped<IWebAuthenticationRepository, WebAuthenticationRepository>();
        services.AddScoped<IWebPasswordRecoveryRepository, WebPasswordRecoveryRepository>();
        services.AddSingleton<PasswordHasher<UserAccount>>();
        services.AddSingleton<MicrosoftWebPasswordVerifier>();
        services.AddSingleton<IWebPasswordVerifier>(provider => provider.GetRequiredService<MicrosoftWebPasswordVerifier>());
        services.AddSingleton<IWebPasswordHasher>(provider => provider.GetRequiredService<MicrosoftWebPasswordVerifier>());
        services.AddScoped<IEmployeeMobileProvisioningRepository, EmployeeMobileProvisioningRepository>();
        services.AddAuthenticationProviders(configuration);
        services.AddAccessManagement();
        var mobileOptions = configuration.GetSection("MobileAuthentication").Get<MobileAuthenticationOptions>() ?? new();
        if (!mobileOptions.IsValid()) throw new InvalidOperationException("MobileAuthentication requires explicit valid security settings.");
        if (mobileOptions.Enabled && int.TryParse(configuration["MobileAuthentication:Crypto:MaxDeliveryLifetimeSeconds"], out var deliveryLifetime) &&
            deliveryLifetime > 0 && deliveryLifetime < mobileOptions.OtpLifetimeSeconds)
            throw new InvalidOperationException("Encrypted delivery lifetime must cover the configured OTP lifetime.");
        services.AddSingleton(mobileOptions);
        var webOptions = configuration.GetSection("WebAuthentication").Get<WebAuthenticationOptions>() ?? new();
        if (!webOptions.IsValid() || (webOptions.Enabled && !mobileOptions.Enabled))
            throw new InvalidOperationException("WebAuthentication requires valid password settings and shared SMS authentication configuration.");
        services.AddSingleton(webOptions);

        services.AddScoped<ILocationConsentRepository, LocationConsentRepository>();
        services.AddScoped<LocationConsentService>();
        services.AddSingleton(configuration.GetSection(PunchRetentionOptions.SectionName).Get<PunchRetentionOptions>() ?? new());
        services.AddScoped<IPunchRetentionRepository, PunchRetentionRepository>();
        services.AddScoped<PunchRetentionService>();
        services.AddHostedService<PunchRetentionWorker>();
        services.AddScoped<IPayrollAccountingRepository, PayrollAccountingRepository>();
        services.AddScoped<PayrollAccountingService>();
        services.AddScoped<IStatutoryContributionRepository, StatutoryContributionRepository>();
        services.AddScoped<StatutoryContributionService>();

        return services;
    }
}
