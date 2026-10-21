using System.Globalization;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Reports;

namespace Cosmix.Application.HR.Reports;

public sealed partial class HrReportService(
    IHrReportRepository repository,
    HrReportAuthorization authorization,
    TimeProvider clock,
    Cosmix.Application.Attendance.Overtime.OvertimeService? overtime = null,
    Cosmix.Application.Attendance.Common.AttendanceOperation? attendance = null)
{
    private const string TimeZoneName = "Asia/Karachi";

    public Task<object> GetReportDataAsync(string reportType, HrReportFilter filter, AuthRequestContext context, CancellationToken ct)
    {
        ValidateReportType(reportType);
        reportType = reportType.ToLowerInvariant();
        filter = HrReportAuthorization.Normalize(reportType, filter, clock.GetUtcNow());
        return authorization.ReadAsync(reportType, filter, false, null, context,
            (selection, token) => ReadDataAsync(reportType, filter, selection, token), ct);
    }

    private async Task<object> ReadDataAsync(string reportType, HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        switch (reportType.ToLowerInvariant())
        {
            case HrReportTypes.DailyAttendance:
            {
                var rows = await repository.GetDailyAttendanceAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Daily Attendance Report", now, TimeZoneName,
                    rows is HrReportRows<DailyAttendanceReportRow> page ? page.TotalCount : rows.Count);
                return new HrReportEnvelope<DailyAttendanceReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.MonthlyAttendanceRegister:
            {
                var rows = await repository.GetMonthlyAttendanceRegisterAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Monthly Attendance Register", now, TimeZoneName,
                    rows is HrReportRows<MonthlyAttendanceRegisterReportRow> page ? page.TotalCount : rows.Count);
                return new HrReportEnvelope<MonthlyAttendanceRegisterReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.PunchAudit:
            {
                var rows = await repository.GetPunchAuditAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Punch Audit Trail Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<PunchAuditReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.OperationalZones:
            {
                var rows = await repository.GetOperationalZonesAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Operational Zones Monitoring Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<OperationalZoneReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.SiteReporting:
            {
                var rows = await repository.GetSiteReportingAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Operational Site Reporting Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<SiteReportingReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.LateKpi:
            {
                var rows = await repository.GetLateKpiAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Late Arrivals & KPI Policy Assessment Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<LateKpiReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.Overtime:
            {
                var rows = await OvertimeRowsAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Overtime Requests & Calculations Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<OvertimeReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.MealAllowance:
            {
                var rows = await repository.GetMealAllowanceAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Meal & Outstation Allowances Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<MealAllowanceReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.Leave:
            {
                var rows = await repository.GetLeaveAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Leave Balances & Utilization Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<LeaveReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.EmployeeMaster:
            {
                var rows = await repository.GetEmployeeMasterAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Employee Master Directory Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<EmployeeMasterReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.DocumentExpiry:
            {
                var rows = await repository.GetDocumentExpiryAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Document & Certification Expiry Reminders Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<DocumentExpiryReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.FuelMileage:
            {
                var rows = await repository.GetFuelMileageAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Vehicle Fuel & Mileage Audit Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<FuelMileageReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.PayrollHandoff:
            {
                var rows = await repository.GetPayrollHandoffAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "Payroll Handoff Financial Summary Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<PayrollHandoffReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            case HrReportTypes.Audit:
            {
                var rows = await repository.GetAuditAsync(filter, selection, ct);
                var meta = new HrReportMetadata(reportType, "System Audit & Compliance Trail Report", now, TimeZoneName, ReportCount(rows));
                return new HrReportEnvelope<AuditReportRow>(meta with { Filter = filter, ReturnedCount = rows.Count }, rows);
            }
            default:
                throw new ArgumentException($"Unsupported report type '{reportType}'.");
        }
    }

    public Task<HrReportDownload> ExportReportAsync(string reportType, HrReportExportRequest request, AuthRequestContext context, CancellationToken ct)
    {
        ValidateReportType(reportType);
        if (request is null || request.Format is not (ReportFormat.Csv or ReportFormat.Xlsx))
            throw AttendanceOperation.Invalid("invalid_hr_report_format");
        reportType = reportType.ToLowerInvariant();
        var filter = HrReportAuthorization.Normalize(reportType, request.Filter ?? new(), clock.GetUtcNow());
        return authorization.ReadAsync(reportType, filter, true, request.Reason, context,
            (selection, token) => ExportAsync(reportType, request, filter, selection, token), ct);
    }

    private async Task<HrReportDownload> ExportAsync(string reportType, HrReportExportRequest request,
        HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        var (headers, rows, title) = await ExtractTabularDataAsync(reportType, filter, selection, ct);

        var now = clock.GetUtcNow();
        var metadata = new HrReportMetadata(reportType, title, now, TimeZoneName, ReportCount(rows),
            Filter: filter, ReturnedCount: rows.Count);

        byte[] content;
        string contentType;
        string extension;

        if (request.Format == ReportFormat.Xlsx)
        {
            content = HrReportXlsxExporter.Generate(metadata, headers, rows);
            contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            extension = ".xlsx";
        }
        else if (request.Format == ReportFormat.Csv)
        {
            content = HrReportCsvExporter.Generate(metadata, headers, rows);
            contentType = "text/csv; charset=utf-8";
            extension = ".csv";
        }
        else
        {
            throw new ArgumentException($"Export format '{request.Format}' is not supported for HR reports.");
        }

        var fileName = $"{reportType}_{now:yyyyMMdd_HHmmss}{extension}";
        return new HrReportDownload(content, contentType, fileName);
    }

    private async Task<(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, string Title)> ExtractTabularDataAsync(
        string reportType, HrReportFilter filter, HrReportSelection selection, CancellationToken ct)
    {
        switch (reportType.ToLowerInvariant())
        {
            case HrReportTypes.DailyAttendance:
            {
                var data = await repository.GetDailyAttendanceAsync(filter, selection, ct);
                string[] headers = ["Employee #", "Full Name", "Department", "Designation", "Site", "Date", "Status", "First In", "Last Out", "Scheduled (m)", "Worked (m)", "Late (m)", "Early Leave (m)", "Overtime (m)", "Exceptions", "Source"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.EmployeeNumber.ToString(), x.FullName, x.DepartmentName, x.DesignationName, x.SiteName,
                    x.Date.ToString("yyyy-MM-dd"), x.Status, x.FirstInUtc ?? "", x.LastOutUtc ?? "",
                    x.ScheduledMinutes?.ToString(CultureInfo.InvariantCulture) ?? "", x.WorkedMinutes?.ToString(CultureInfo.InvariantCulture) ?? "",
                    x.LateMinutes?.ToString(CultureInfo.InvariantCulture) ?? "", x.EarlyLeaveMinutes?.ToString(CultureInfo.InvariantCulture) ?? "",
                    x.OvertimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? "", x.ExceptionCount?.ToString(CultureInfo.InvariantCulture) ?? "", x.Source
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Daily Attendance Report");
            }
            case HrReportTypes.MonthlyAttendanceRegister:
            {
                var data = await repository.GetMonthlyAttendanceRegisterAsync(filter, selection, ct);
                string[] headers = ["Employee #", "Full Name", "Department", "Designation", "Site", "Month-Year", "Total Days", "Present", "Late", "Absent", "Leave", "Rest/Holiday", "Worked Hours", "Payable Hours", "Overtime Hours", "Summary Days", "Missing Summary Days", "Source"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.EmployeeNumber.ToString(), x.FullName, x.DepartmentName, x.DesignationName, x.SiteName,
                    x.MonthYear, x.TotalCalendarDays.ToString(), x.PresentDays.ToString(), x.LateDays.ToString(),
                    x.AbsentDays.ToString(), x.LeaveDays.ToString(), x.RestHolidayDays.ToString(),
                    x.TotalWorkedHours.ToString("F2", CultureInfo.InvariantCulture),
                    x.TotalPayableHours.ToString("F2", CultureInfo.InvariantCulture),
                    x.TotalOvertimeHours.ToString("F2", CultureInfo.InvariantCulture), x.SummaryDays.ToString(CultureInfo.InvariantCulture),
                    x.MissingSummaryDays.ToString(CultureInfo.InvariantCulture), x.Source
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Monthly Attendance Register");
            }
            case HrReportTypes.PunchAudit:
            {
                var data = await repository.GetPunchAuditAsync(filter, selection, ct);
                string[] headers = ["Punch ID", "Received (UTC)", "Device Captured (UTC)", "Employee #", "Full Name", "Action", "Source", "Device ID", "Site", "Offline", "Latitude", "Longitude", "Accuracy (m)", "In Geofence", "Assessment", "Exception Reason"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.PunchId.ToString(), x.ReceivedAtUtc.ToString("O", CultureInfo.InvariantCulture), x.DeviceCapturedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "",
                    x.EmployeeNumber.ToString(), x.FullName, x.Action, x.Source, x.RegisteredDeviceId, x.SiteName,
                    x.IsOffline ? "Yes" : "No", x.Latitude?.ToString(CultureInfo.InvariantCulture) ?? "", x.Longitude?.ToString(CultureInfo.InvariantCulture) ?? "",
                    x.AccuracyMeters?.ToString(CultureInfo.InvariantCulture) ?? "", x.IsWithinGeofence.HasValue ? x.IsWithinGeofence.Value ? "Yes" : "No" : "", x.AssessmentStatus, x.ExceptionReason ?? ""
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Punch Audit Trail Report");
            }
            case HrReportTypes.OperationalZones:
            {
                var data = await repository.GetOperationalZonesAsync(filter, selection, ct);
                string[] headers = ["Event ID", "Timestamp (UTC)", "Employee #", "Full Name", "Zone Code", "Zone Name", "Zone Color", "Event Kind", "Source", "Instruction", "Offline", "Review Status"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.EventId.ToString(), x.ReceivedAtUtc.ToString("O", CultureInfo.InvariantCulture), x.EmployeeNumber.ToString(), x.FullName,
                    x.ZoneCode, x.ZoneName, x.ZoneColor, x.EventKind, x.Source, x.ShownInstruction ?? "", x.IsOffline ? "Yes" : "No", x.ReviewStatus
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Operational Zones Monitoring Report");
            }
            case HrReportTypes.SiteReporting:
            {
                var data = await repository.GetSiteReportingAsync(filter, selection, ct);
                string[] headers = ["Report ID", "Reported (UTC)", "Employee #", "Reporter", "Site", "Kind", "Task Ref", "Notes", "Offline", "Review Decision", "Reviewed By", "Reviewed (UTC)", "Reviewer Notes", "Latest Response ID", "Latest Response (UTC)"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.ReportId.ToString(), x.ReceivedAtUtc.ToString("O", CultureInfo.InvariantCulture), x.EmployeeNumber.ToString(), x.ReporterName,
                    x.SiteName, x.ReportKind, x.TaskReference ?? "", x.Notes, x.IsOffline ? "Yes" : "No",
                    x.LatestReviewDecision, x.ReviewedByUserId ?? "", x.ReviewedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "", x.ReviewerNotes ?? "", x.LatestResponseId?.ToString() ?? "", x.LatestResponseAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? ""
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Operational Site Reporting Report");
            }
            case HrReportTypes.LateKpi:
            {
                var data = await repository.GetLateKpiAsync(filter, selection, ct);
                string[] headers = ["Assessment ID", "Employee #", "Full Name", "Department", "Policy Year", "Period Start", "Period End", "Impact Kind", "Affected Minutes", "Proposed Money Impact", "Currency", "KPI % Impact", "Disciplinary Action", "Calculation Version", "Assessed (UTC)"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.AssessmentId.ToString(), x.EmployeeNumber.ToString(), x.FullName, x.DepartmentName, x.PolicyYear.ToString(),
                    x.PeriodStart.ToString("yyyy-MM-dd"), x.PeriodEnd.ToString("yyyy-MM-dd"), x.ImpactKind, x.AffectedMinutes.ToString(),
                    x.ProposedMoneyImpact?.ToString(CultureInfo.InvariantCulture) ?? "", x.Currency ?? "",
                    x.ProposedKpiPercent?.ToString(CultureInfo.InvariantCulture) ?? "", x.DisciplinaryAction,
                    x.CalculationVersion, x.AssessedAtUtc.ToString("O", CultureInfo.InvariantCulture)
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Late Arrivals & KPI Policy Assessment Report");
            }
            case HrReportTypes.Overtime:
            {
                var data = await OvertimeRowsAsync(filter, selection, ct);
                string[] headers = ["Request ID", "Requested (UTC)", "Employee #", "Full Name", "Site", "Starts (UTC)", "Ends (UTC)", "Requested (m)", "Approved (m)", "Payable (PKR)", "Rate Code", "Job Ref", "Reason", "Requested By", "Status", "Decision ID", "Approved Ticks", "Currency", "Financial State"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.RequestId.ToString(), x.RequestedAtUtc.ToString("O", CultureInfo.InvariantCulture), x.EmployeeNumber.ToString(), x.FullName,
                    x.SiteName, x.StartsAtUtc.ToString("O", CultureInfo.InvariantCulture), x.EndsAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    x.RequestedMinutes.ToString(), x.ApprovedMinutes?.ToString(CultureInfo.InvariantCulture) ?? "", x.PayableAmount?.ToString(CultureInfo.InvariantCulture) ?? "",
                    x.RateRuleCode ?? "", x.JobReference ?? "", x.Reason, x.RequestedBy, x.ApprovalStatus, x.DecisionId?.ToString() ?? "", x.ApprovedTicks?.ToString(CultureInfo.InvariantCulture) ?? "", x.Currency ?? "", x.FinancialState
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Overtime Requests & Calculations Report");
            }
            case HrReportTypes.MealAllowance:
            {
                var data = await repository.GetMealAllowanceAsync(filter, selection, ct);
                string[] headers = ["Claim ID", "Created (UTC)", "Employee #", "Full Name", "Allowance Kind", "Deployment Category", "Period Start", "Period End", "Claimed Amount", "Currency", "Payable Date", "Payroll Period", "Approval", "Settlement", "Batch Ref"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.ClaimId.ToString(), x.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture), x.EmployeeNumber.ToString(), x.FullName,
                    x.AllowanceKind, x.DeploymentCategoryCode, x.EligibilityPeriodStart.ToString("O", CultureInfo.InvariantCulture),
                    x.EligibilityPeriodEnd.ToString("O", CultureInfo.InvariantCulture), x.ClaimedAmount.ToString(CultureInfo.InvariantCulture),
                    x.Currency, x.PayableDate.ToString("yyyy-MM-dd"), x.PayrollPeriodStart?.ToString("yyyy-MM-dd") ?? "",
                    x.ApprovalStatus, x.SettlementStatus, x.SettlementBatchReference ?? ""
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Meal & Outstation Allowances Report");
            }
            case HrReportTypes.Leave:
            {
                var data = await repository.GetLeaveAsync(filter, selection, ct);
                string[] headers = ["Employee #", "Full Name", "Leave Type", "Year", "Entitled Days", "Used Days", "Pending Days", "Balance Days", "Encashable Days", "Encashed Settled", "Unpaid Days", "Leave Type ID", "Fiscal Start", "Fiscal End (Exclusive)", "Entitled Minutes", "Used Minutes", "Reserved Minutes", "Balance Minutes", "Encashment Minutes", "Source"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.EmployeeNumber.ToString(), x.FullName, x.LeaveTypeName, x.LeaveYear.ToString(),
                    x.EntitledDays?.ToString(CultureInfo.InvariantCulture) ?? "", x.UsedDays?.ToString(CultureInfo.InvariantCulture) ?? "",
                    x.PendingDays?.ToString(CultureInfo.InvariantCulture) ?? "", x.BalanceDays?.ToString(CultureInfo.InvariantCulture) ?? "",
                    x.EncashableDays?.ToString(CultureInfo.InvariantCulture) ?? "", x.EncashedSettledDays?.ToString(CultureInfo.InvariantCulture) ?? "",
                    x.UnpaidLeaveDays?.ToString(CultureInfo.InvariantCulture) ?? "", x.LeaveTypeId.ToString(),
                    x.LeaveYearStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.LeaveYearEndExclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    x.EntitledMinutes.ToString(CultureInfo.InvariantCulture), x.UsedMinutes.ToString(CultureInfo.InvariantCulture),
                    x.ReservedMinutes.ToString(CultureInfo.InvariantCulture), x.BalanceMinutes.ToString(CultureInfo.InvariantCulture),
                    x.EncashmentMinutes.ToString(CultureInfo.InvariantCulture), x.Source
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Leave Balances & Utilization Report");
            }
            case HrReportTypes.EmployeeMaster:
            {
                var data = await repository.GetEmployeeMasterAsync(filter, selection, ct);
                string[] headers = ["Employee #", "Full Name", "Father/Spouse Name", "Date of Birth", "Gender", "Department", "Designation", "Branch", "Staff Category", "Employment Type", "Joined Date", "Status", "Official Email", "Company Phone"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.EmployeeNumber.ToString(), x.FullName, x.FatherOrSpouseName, x.DateOfBirth?.ToString("yyyy-MM-dd") ?? "",
                    x.Gender, x.DepartmentName, x.DesignationName, x.BranchName, x.StaffCategoryName, x.EmploymentTypeName,
                    x.JoinedOn?.ToString("yyyy-MM-dd") ?? "", x.Status, x.OfficialEmail, x.CompanyPhone
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Employee Master Directory Report");
            }
            case HrReportTypes.DocumentExpiry:
            {
                var data = await repository.GetDocumentExpiryAsync(filter, selection, ct);
                string[] headers = ["Employee #", "Full Name", "Category", "Document Type", "Document Number", "Issuer", "Issued On", "Expires On", "Days Remaining", "Status"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.EmployeeNumber.ToString(), x.FullName, x.Category, x.DocumentType, x.DocumentNumber, x.Issuer,
                    x.IssuedOn?.ToString("yyyy-MM-dd") ?? "", x.ExpiresOn?.ToString("yyyy-MM-dd") ?? "",
                    x.DaysUntilExpiry.ToString(), x.Status
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Document & Certification Expiry Reminders Report");
            }
            case HrReportTypes.FuelMileage:
            {
                var data = await repository.GetFuelMileageAsync(filter, selection, ct);
                string[] headers = ["Record ID", "Trip Date", "Vehicle Reg #", "Make & Model", "Employee #", "Driver Name", "Approved Route", "Eligible KM", "Commute KM", "Fuel Liters", "KM / Liter", "Claimed Amount", "Currency", "Approval", "Next Service Due KM"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.RecordId.ToString(), x.TripDate?.ToString("yyyy-MM-dd") ?? "", x.RegistrationNumber, x.MakeModel,
                    x.EmployeeNumber.ToString(), x.DriverName, x.ApprovedRoute, x.EligibleDistanceKm.ToString(CultureInfo.InvariantCulture),
                    x.ExcludedCommuteKm.ToString(CultureInfo.InvariantCulture), x.FuelLiters.ToString(CultureInfo.InvariantCulture),
                    x.KmPerLiter?.ToString(CultureInfo.InvariantCulture) ?? "", x.ClaimedAmount.ToString(CultureInfo.InvariantCulture),
                    x.Currency, x.ApprovalStatus, x.NextServiceDueKm?.ToString(CultureInfo.InvariantCulture) ?? ""
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Vehicle Fuel & Mileage Audit Report");
            }
            case HrReportTypes.PayrollHandoff:
            {
                var data = await repository.GetPayrollHandoffAsync(filter, selection, ct);
                string[] headers = ["Batch ID", "Batch Ref", "Line ID", "Employee #", "Full Name", "Period Start", "Period End", "Component", "Source Type", "Amount", "Currency", "Trace ID", "Locked", "Locked (UTC)", "Source ID", "Source Version", "Attendance Lock Event ID"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.BatchId.ToString(), x.BatchReference, x.LineId.ToString(), x.EmployeeNumber.ToString(), x.FullName,
                    x.PeriodStart.ToString("yyyy-MM-dd"), x.PeriodEnd.ToString("yyyy-MM-dd"), x.ComponentKind, x.SourceType,
                    x.Amount.ToString(CultureInfo.InvariantCulture), x.Currency, x.TraceId, x.IsBatchLocked ? "Yes" : "No",
                    x.LockedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "", x.SourceId.ToString(), x.SourceVersion.ToString(CultureInfo.InvariantCulture), x.AttendancePeriodLockEventId.ToString()
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "Payroll Handoff Financial Summary Report");
            }
            case HrReportTypes.Audit:
            {
                var data = await repository.GetAuditAsync(filter, selection, ct);
                string[] headers = ["Event ID", "Occurred (UTC)", "Actor User ID", "Role", "Event Type", "Target Type", "Target ID", "Scope Employee #", "Scope Employee Name", "Correlation ID", "IP Address", "Device Ref"];
                var rows = data.Select(x => (IReadOnlyList<string>)[
                    x.EventId.ToString(), x.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture), x.ActorUserId, x.ActorRoleCode,
                    x.EventType, x.TargetType, x.TargetId, x.ScopeEmployeeNumber?.ToString() ?? "", x.ScopeEmployeeName ?? "",
                    x.CorrelationId, x.IpAddress ?? "", x.DeviceReference ?? ""
                ]).ToList();
                return (headers, new HrReportRows<IReadOnlyList<string>>(rows, ExportCount(data, selection)), "System Audit & Compliance Trail Report");
            }
            default:
                throw new ArgumentException($"Unsupported report type '{reportType}'.");
        }
    }

    private static int ReportCount<T>(IReadOnlyList<T> rows) => rows is HrReportRows<T> page ? page.TotalCount : rows.Count;

    private static void ValidateReportType(string reportType)
    {
        if (string.IsNullOrWhiteSpace(reportType) || !HrReportTypes.IsValid(reportType))
        {
            throw AttendanceOperation.Invalid("invalid_hr_report_type");
        }
    }
}
