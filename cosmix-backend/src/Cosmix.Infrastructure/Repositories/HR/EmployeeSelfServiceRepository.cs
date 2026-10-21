using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.EmployeeSelfService;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed partial class EmployeeSelfServiceRepository(CosmixDbContext db, TimeProvider clock) : IEmployeeSelfServiceRepository, IEmployeePayrollHistoryReader
{
    public async Task<EssProfileResponse?> GetProfileAsync(Guid employeeId, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

        if (employee is null)
            return null;

        var today = RosterSchedule.Today(clock.GetUtcNow());

        EmployeeAssignment? effectiveAssignment = null;
        var assignmentStatus = "unavailable";
        try
        {
            effectiveAssignment = await new OfficeRosterRepository(db).EmploymentAsync(employeeId, today, ct);
            if (effectiveAssignment is not null) assignmentStatus = "available";
        }
        catch (AuthenticationFailure failure) when (failure.Code == "employee_assignment_history_conflict")
        { assignmentStatus = "conflict"; }

        var activeAssignment = effectiveAssignment is null ? null : await db.EmployeeAssignments.AsNoTracking()
            .Include(a => a.Department)
            .Include(a => a.Designation)
            .Include(a => a.StaffCategory)
            .Include(a => a.EmploymentType)
            .Include(a => a.Branch)
            .Include(a => a.Site)
            .Include(a => a.ReportingManager)
            .SingleAsync(a => a.Id == effectiveAssignment.Id && a.EmployeeId == employeeId, ct);

        var documents = await db.EmployeeDocuments.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new EssDocumentSummary(
                d.Id,
                d.DocumentType.ToString(),
                d.OriginalFileName,
                d.ContentType,
                d.SizeBytes,
                d.IssuedOn,
                d.ExpiresOn,
                d.ScanStatus.ToString(),
                d.VerificationStatus.ToString(),
                d.VerificationStatus == DocumentVerificationStatus.Verified
            ))
            .ToListAsync(ct);

        var assets = await db.EmployeeAssetCustodies.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && !a.ReturnedAt.HasValue)
            .OrderByDescending(a => a.IssuedAt)
            .Select(a => new EssAssetSummary(
                a.Id,
                a.AssetReference,
                a.IssuedAt,
                a.IssueCondition,
                a.ReturnedAt,
                a.ReturnCondition,
                !a.ReturnedAt.HasValue
            ))
            .ToListAsync(ct);

        var sims = await db.EmployeeSimAssignments.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.Status != CompanySimStatus.Returned)
            .OrderByDescending(s => s.IssuedOn)
            .Select(s => new EssSimSummary(
                s.Id,
                s.PhoneNumber,
                s.SimReference,
                s.AllowedCreditTierCode,
                s.Status.ToString(),
                s.IssuedOn,
                s.ReturnedOn,
                s.Status != CompanySimStatus.Returned
            ))
            .ToListAsync(ct);

        EssWorkAssignmentInfo? assignmentInfo = null;
        if (activeAssignment is not null)
        {
            assignmentInfo = new EssWorkAssignmentInfo(
                activeAssignment.Id,
                activeAssignment.Department?.Name ?? "Unassigned",
                activeAssignment.Designation?.Name ?? "Unassigned",
                activeAssignment.StaffCategory?.Name ?? "Unassigned",
                activeAssignment.EmploymentType?.Name ?? "Unassigned",
                activeAssignment.Branch?.Name,
                activeAssignment.Site?.Name,
                activeAssignment.ReportingManagerId,
                activeAssignment.ReportingManager?.FullLegalName,
                activeAssignment.ReportingManager?.EmployeeNumber,
                activeAssignment.EffectiveFrom,
                activeAssignment.EffectiveTo
            );
        }

        return new EssProfileResponse(
            employee.Id,
            employee.EmployeeNumber,
            employee.FullLegalName ?? string.Empty,
            employee.FullLegalName,
            employee.FatherOrSpouseName,
            employee.IdentityDocumentType?.ToString(),
            employee.IdentityDocumentNumber,
            employee.IdentityDocumentExpiresOn,
            employee.DateOfBirth,
            employee.Gender,
            employee.MaritalStatus,
            employee.PersonalPhone,
            employee.CompanyPhone,
            employee.PersonalEmail,
            employee.OfficialEmail,
            employee.JoinedOn,
            employee.ProbationEndsOn,
            employee.Status.ToString(),
            employee.IsArchived,
            assignmentInfo,
            documents,
            assets,
            sims
        )
        {
            AssignmentAsOf = today,
            AssignmentStatus = assignmentStatus
        };
    }

    public async Task<EssLeaveRequestSelection> GetLeaveRequestIdsAsync(Guid employeeId, DateOnly today, CancellationToken ct)
    {
        var own = db.LeaveRequests.AsNoTracking().Where(r => r.EmployeeId == employeeId);
        var recent = await own.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Select(r => r.Id).Take(20).ToArrayAsync(ct);
        // Filter cancellation/supersession before limiting, so removed requests cannot hide later leave.
        // Shared LeaveService supplies exact allocations, decisions and cancellation history.
        var upcoming = await own.Where(r => r.StartDate >= today && r.ApprovalRequest != null &&
                r.ApprovalRequest.State == ApprovalState.Approved &&
                !db.LeaveRequests.Any(next => next.PreviousRequestId == r.Id) &&
                !db.LeaveCancellationRequests.Any(c => c.LeaveRequestId == r.Id &&
                    c.ApprovalRequest != null && c.ApprovalRequest.State == ApprovalState.Approved))
            .OrderBy(r => r.StartDate).ThenBy(r => r.Id).Select(r => r.Id).Take(5).ToArrayAsync(ct);
        return new(recent, upcoming);
    }

    public async Task<EssFinancialOverviewResponse> GetFinancialOverviewAsync(Guid employeeId, CancellationToken ct)
    {
        var today = RosterSchedule.Today(clock.GetUtcNow());
        var comp = await db.CompensationRecords.AsNoTracking()
            .Where(c => c.EmployeeId == employeeId && c.EffectiveFrom <= today &&
                (!c.EffectiveTo.HasValue || today < c.EffectiveTo.Value))
            .OrderByDescending(c => c.EffectiveFrom)
            .ThenByDescending(c => c.Revision)
            .FirstOrDefaultAsync(ct);

        EssCompensationSummary? compSummary = null;
        if (comp is not null)
        {
            compSummary = new EssCompensationSummary(
                comp.BaseAmount,
                comp.GrossAmount,
                comp.Currency,
                comp.WageBasis.ToString(),
                comp.PaymentMethod.ToString(),
                comp.EffectiveFrom
            );
        }

        var agreementsRaw = await db.AdvanceLoanAgreements.AsNoTracking()
            .Include(a => a.AdvanceLoanRequest)
            .Include(a => a.Schedule)
            .Include(a => a.ApprovalRequest)
            .Where(a => a.AdvanceLoanRequest.EmployeeId == employeeId)
            .OrderByDescending(a => a.EffectiveOn)
            .ToListAsync(ct);

        var agreementIds = agreementsRaw.Select(a => a.Id).ToList();
        var repaidMap = await db.RepaymentLedgerEntries.AsNoTracking()
            .Where(r => agreementIds.Contains(r.AdvanceLoanAgreementId))
            .GroupBy(r => r.AdvanceLoanAgreementId)
            .Select(g => new
            {
                AgreementId = g.Key,
                TotalRepaid = g.Sum(r => r.Amount)
            })
            .ToDictionaryAsync(x => x.AgreementId, x => x.TotalRepaid, ct);

        var recentRepaymentsRaw = await db.RepaymentLedgerEntries.AsNoTracking()
            .Include(r => r.AdvanceLoanAgreement)
            .ThenInclude(a => a.AdvanceLoanRequest)
            .Where(r => agreementIds.Contains(r.AdvanceLoanAgreementId))
            .OrderByDescending(r => r.ValueDate)
            .Take(20)
            .ToListAsync(ct);

        var agreements = agreementsRaw.Select(a =>
        {
            var repaid = repaidMap.GetValueOrDefault(a.Id, 0m);
            var remaining = Math.Max(0, a.ApprovedPrincipal - repaid);
            var monthlyInstallment = a.Schedule.Count > 0 ? a.Schedule.First().PrincipalAmount : 0;
            var nextDueDate = a.Schedule.Where(s => s.DueDate >= today).OrderBy(s => s.DueDate).Select(s => (DateOnly?)s.DueDate).FirstOrDefault();
            var statusStr = a.ApprovalRequest.State == ApprovalState.Approved ? (remaining == 0 ? "Settled" : "Active") : a.ApprovalRequest.State.ToString();

            return new EssAdvanceLoanAgreementSummary(
                a.Id,
                $"AGR-{a.Id.ToString("N")[..8].ToUpperInvariant()}",
                a.AdvanceLoanRequest.Kind.ToString(),
                a.ApprovedPrincipal,
                a.Currency,
                a.Schedule.Count,
                monthlyInstallment,
                repaid,
                remaining,
                statusStr,
                a.EffectiveOn,
                a.Schedule.Count > 0 ? a.Schedule.Max(s => s.DueDate) : a.EffectiveOn,
                nextDueDate
            );
        }).ToList();

        var repayments = recentRepaymentsRaw.Select(r => new EssRepaymentHistoryItem(
            r.Id,
            r.AdvanceLoanAgreementId,
            $"AGR-{r.AdvanceLoanAgreementId.ToString("N")[..8].ToUpperInvariant()}",
            r.Amount,
            r.Currency,
            r.SourceReference,
            r.Kind.ToString(),
            r.ValueDate
        )).ToList();

        var allowancesRaw = await db.AllowanceClaims.AsNoTracking()
            .Include(a => a.ApprovalRequest)
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.PayableDate)
            .Take(20)
            .ToListAsync(ct);

        var allowances = allowancesRaw.Select(a => new EssAllowanceClaimSummary(
            a.Id,
            a.EligibilityKey,
            a.Kind.ToString(),
            a.ClaimedAmount,
            a.Currency,
            a.PayableDate,
            (decimal)(a.EligibilityPeriodEnd - a.EligibilityPeriodStart).TotalHours,
            a.SettlementState == ClaimSettlementState.RecordedAsPaid ? "Paid" :
                a.SettlementState == ClaimSettlementState.InPayrollHandoff ? "InPayrollHandoff" :
                (a.ApprovalRequest != null ? a.ApprovalRequest.State.ToString() : "Submitted"),
            a.CreatedAt
        )).ToList();

        var fuelRaw = await db.FuelClaims.AsNoTracking()
            .Include(f => f.ApprovalRequest)
            .Include(f => f.VehicleTrip)
            .Where(f => f.EmployeeId == employeeId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var fuel = fuelRaw.Select(f => new EssFuelClaimSummary(
            f.Id,
            f.VehicleTrip != null ? f.VehicleTrip.WorkReference : f.Id.ToString("N")[..8],
            f.VehicleTrip != null ? DateOnly.FromDateTime(f.VehicleTrip.StartedAt.DateTime) : DateOnly.FromDateTime(f.CreatedAt.DateTime),
            f.VehicleTrip != null ? f.VehicleTrip.OdometerEndKm : 0,
            f.FuelLiters,
            f.ClaimedAmount,
            f.ApprovalRequest != null ? f.ApprovalRequest.State.ToString() : "Submitted",
            f.CreatedAt
        )).ToList();

        var custody = await db.VehicleCustodies.AsNoTracking()
            .Include(c => c.Vehicle)
            .Where(c => c.EmployeeId == employeeId && !c.ReturnedAt.HasValue)
            .OrderByDescending(c => c.IssuedAt)
            .FirstOrDefaultAsync(ct);

        EssVehicleInfo? vehicleInfo = null;
        if (custody?.Vehicle is not null)
        {
            vehicleInfo = new EssVehicleInfo(
                custody.Vehicle.Id,
                custody.Vehicle.RegistrationNumber,
                custody.Vehicle.VehicleType,
                custody.Vehicle.MakeModel,
                custody.Vehicle.AssetReference,
                custody.IssueOdometerKm,
                custody.IssuedAt
            );
        }

        var payrollBatches = await db.PayrollHandoffLines.AsNoTracking()
            .Include(p => p.PayrollHandoffBatch)
            .Where(p => p.EmployeeId == employeeId)
            .GroupBy(p => p.PayrollHandoffBatch)
            .Select(g => new
            {
                Batch = g.Key,
                TotalEarnings = g.Where(x => x.Component == PayrollComponentKind.BaseCompensation || x.Component == PayrollComponentKind.Overtime || x.Component == PayrollComponentKind.Allowance || x.Component == PayrollComponentKind.FuelClaim).Sum(x => x.Amount),
                TotalDeductions = g.Where(x => x.Component == PayrollComponentKind.AttendanceDeduction || x.Component == PayrollComponentKind.LeaveAdjustment || x.Component == PayrollComponentKind.LoanRepayment).Sum(x => x.Amount),
                Net = g.Sum(x => x.Amount),
                Currency = g.Select(x => x.Currency).FirstOrDefault() ?? "PKR"
            })
            .OrderByDescending(x => x.Batch.PeriodEnd)
            .Take(6)
            .ToListAsync(ct);

        var payrollSummaries = payrollBatches.Select(p => new EssPayrollHandoffSummary(
            p.Batch.Id,
            p.Batch.BatchReference,
            p.Batch.PeriodStart,
            p.Batch.PeriodEnd,
            p.TotalEarnings,
            p.TotalDeductions,
            p.Net,
            p.Currency,
            p.Batch.PreparedAt
        )).ToList();

        return new EssFinancialOverviewResponse(
            compSummary,
            agreements,
            repayments,
            allowances,
            fuel,
            vehicleInfo,
            payrollSummaries
        );
    }

    public async Task<EssRequestsSummaryResponse> GetRequestsSummaryAsync(Guid employeeId, CancellationToken ct)
    {
        var pendingLeaves = await db.LeaveRequests.AsNoTracking()
            .CountAsync(r => r.EmployeeId == employeeId && r.ApprovalRequest != null &&
                r.ApprovalRequest.State == ApprovalState.Pending &&
                !db.LeaveRequests.Any(next => next.PreviousRequestId == r.Id), ct);

        var pendingAdvances = await db.AdvanceLoanRequests.AsNoTracking()
            .CountAsync(a => a.EmployeeId == employeeId && (a.ApprovalRequest == null || a.ApprovalRequest.State == ApprovalState.Pending), ct);

        var pendingAllowances = await db.AllowanceClaims.AsNoTracking()
            .CountAsync(c => c.EmployeeId == employeeId && (c.ApprovalRequest == null || c.ApprovalRequest.State == ApprovalState.Pending), ct);

        var pendingFuel = await db.FuelClaims.AsNoTracking()
            .CountAsync(f => f.EmployeeId == employeeId && (f.ApprovalRequest == null || f.ApprovalRequest.State == ApprovalState.Pending), ct);

        var pendingCorrections = await db.AttendanceCorrections.AsNoTracking()
            .CountAsync(c => c.EmployeeId == employeeId && db.ApprovalRequests.Any(a => a.Id == c.ApprovalRequestId && a.State == ApprovalState.Pending), ct);

        var total = pendingLeaves + pendingAdvances + pendingAllowances + pendingFuel + pendingCorrections;

        return new EssRequestsSummaryResponse(
            pendingLeaves,
            pendingAdvances,
            pendingAllowances,
            pendingFuel,
            pendingCorrections,
            total
        );
    }
}
