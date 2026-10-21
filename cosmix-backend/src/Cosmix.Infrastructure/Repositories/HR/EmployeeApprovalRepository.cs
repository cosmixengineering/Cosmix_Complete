using Cosmix.Application.Authentication;
using Cosmix.Application.HR.Employees;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Repositories.HR;

public sealed class EmployeeApprovalRepository(CosmixDbContext db, MobileAuthenticationOptions options) : IEmployeeApprovalRepository
{
    public void AddDecision(EmployeeOnboardingDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var tracked = db.EmployeeSubmissions.Local.SingleOrDefault(s => s.Id == decision.SubmissionId);
        if (tracked is not null)
            db.Entry(decision).Reference(d => d.Submission).CurrentValue = tracked;
        else
            db.Entry(decision.Submission).State = EntityState.Unchanged;
        // Adding only the decision prevents its detached snapshot graph from being inserted again.
        db.Entry(decision).State = EntityState.Added;
    }

    public async Task<IReadOnlyList<EmployeeOnboardingDecision>> DecisionsAsync(Guid employeeId, CancellationToken ct) =>
        await db.EmployeeOnboardingDecisions.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId && d.Provenance == OnboardingProvenance.HrWorkflow)
            .OrderByDescending(d => d.DecidedAt).ThenByDescending(d => d.Id).Take(100).ToArrayAsync(ct);

    public Task<UserAccount?> FindAccountAsync(Guid employeeId, CancellationToken ct) =>
        db.UserAccounts.SingleOrDefaultAsync(a => a.EmployeeId == employeeId, ct);

    public async Task ValidateInitialSiteAsync(Guid? siteId, DateOnly effectiveFrom, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!siteId.HasValue) return;
        if (siteId == Guid.Empty || !await db.Sites.AsNoTracking().AnyAsync(s => s.Id == siteId && s.IsActive &&
            (!s.StartsOn.HasValue || s.StartsOn <= effectiveFrom) && (!s.EndsOn.HasValue || s.EndsOn >= effectiveFrom), ct))
            throw new AuthenticationFailure("inactive_or_unknown_initial_site", 400);
    }

    public async Task ValidateAssetReturnReceiversAsync(IReadOnlyList<AssetProposalInput> assets, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (assets is null || assets.Count > 100 || assets.Any(a => a is null))
            throw new AuthenticationFailure("invalid_employee_asset_proposal", 400);
        var returns = assets.Where(a => a.ReturnedOn.HasValue).ToArray();
        if (returns.Length == 0) return;
        if (returns.Any(a => !a.ReturnedToAccountId.HasValue || a.ReturnedToAccountId == Guid.Empty))
            throw new AuthenticationFailure("employee_asset_return_incomplete", 400);
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(options.BusinessTimeZoneId); }
        catch (Exception ex) when (ex is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new AuthenticationFailure("business_timezone_not_configured", 503); }
        var ids = returns.Select(a => a.ReturnedToAccountId!.Value).Distinct().ToArray();
        var receivers = await db.UserAccounts.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.CreatedAt }).ToDictionaryAsync(a => a.Id, a => a.CreatedAt, ct);
        // Hiring captures dates, not event times. A receiver may have been created any time that business day.
        // Current account status does not invalidate historical custody evidence.
        if (returns.Any(a => !receivers.TryGetValue(a.ReturnedToAccountId!.Value, out var createdAt) ||
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(createdAt, zone).DateTime) > a.ReturnedOn!.Value))
            throw new AuthenticationFailure("invalid_employee_asset_return_receiver", 400);
    }

    public async Task<bool> HasInitialRecordsAsync(Guid employeeId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (db.ChangeTracker.Entries().Any(e => e.State != EntityState.Detached && IsInitialRecord(e.Entity, employeeId)))
            return true;
        return await db.EmployeeAssignments.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.EmployeeAddresses.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.EmployeeEmergencyContacts.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.PreviousEmployments.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.EmployeeCertifications.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.EmployeeAssetCustodies.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.CompensationRecords.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.EmployeeBankDetails.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.EmployeeMedicalRecords.AnyAsync(x => x.EmployeeId == employeeId, ct) ||
            await db.EmployeeSimAssignments.AnyAsync(x => x.EmployeeId == employeeId, ct);
    }

    public void AddInitialRecords(EmployeeApprovalRecords records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var id = records.Assignment.EmployeeId;
        var employee = db.ChangeTracker.Entries<Employee>().SingleOrDefault(e => e.Entity.Id == id);
        if (employee is null || employee.State is EntityState.Added or EntityState.Deleted ||
            employee.Entity.OnboardingStatus != OnboardingStatus.Approved || employee.Entity.Status != EmployeeStatus.Active ||
            !employee.Entity.ApprovedOnboardingDecisionId.HasValue ||
            employee.Property(e => e.OnboardingStatus).OriginalValue != OnboardingStatus.Approved)
            throw new InvalidOperationException("Save the decision before materializing the tracked approved employee.");
        if (db.ChangeTracker.Entries().Any(e => e.State != EntityState.Detached && IsInitialRecord(e.Entity, id)))
            throw new AuthenticationFailure("employee_already_materialized", 409);

        var rows = new List<IEmployeeProfileRecord> { records.Assignment, records.EmergencyContact };
        rows.AddRange(records.Addresses);
        rows.AddRange(records.PreviousEmployment);
        rows.AddRange(records.Certifications);
        rows.AddRange(records.Assets);
        if (records.Medical is not null) rows.Add(records.Medical);
        if (records.Compensation is not null) rows.Add(records.Compensation);
        if (records.Bank is not null) rows.Add(records.Bank);
        if (rows.Any(r => r.EmployeeId != id))
            throw new InvalidOperationException("Initial records must all belong to the approved employee.");
        db.AddRange(rows);
    }

    private static bool IsInitialRecord(object entity, Guid employeeId) =>
        entity is IEmployeeProfileRecord record && record.EmployeeId == employeeId &&
        entity is EmployeeAssignment or EmployeeAddress or EmployeeEmergencyContact or PreviousEmployment or
            EmployeeCertification or EmployeeAssetCustody or CompensationRecord or EmployeeBankDetails or
            EmployeeMedicalRecord or EmployeeSimAssignment;
}
