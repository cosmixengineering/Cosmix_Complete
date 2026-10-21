namespace Cosmix.Application.HR.Reports;

public interface IHrReportRepository
{
    Task<HrReportSelection> SelectScopeAsync(string reportType, HrReportFilter filter, HrReportAccess access, CancellationToken ct);
    Task<HrReportSelection> SelectRetainedScopeAsync(HrReportExportScope scope, HrReportAccess access, bool includeSources, CancellationToken ct);
    Task<IReadOnlyList<DailyAttendanceReportRow>> GetDailyAttendanceAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<MonthlyAttendanceRegisterReportRow>> GetMonthlyAttendanceRegisterAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<PunchAuditReportRow>> GetPunchAuditAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<OperationalZoneReportRow>> GetOperationalZonesAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<SiteReportingReportRow>> GetSiteReportingAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<LateKpiReportRow>> GetLateKpiAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<OvertimeReportRow>> GetOvertimeAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<MealAllowanceReportRow>> GetMealAllowanceAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<LeaveReportRow>> GetLeaveAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<EmployeeMasterReportRow>> GetEmployeeMasterAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<DocumentExpiryReportRow>> GetDocumentExpiryAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<FuelMileageReportRow>> GetFuelMileageAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<PayrollHandoffReportRow>> GetPayrollHandoffAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
    Task<IReadOnlyList<AuditReportRow>> GetAuditAsync(HrReportFilter filter, HrReportSelection selection, CancellationToken ct);
}
