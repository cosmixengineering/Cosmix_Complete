using Cosmix.Domain.Attendance.Corrections;
using Cosmix.Domain.Attendance.CarryForward;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Attendance.PayrollInputs;
using Cosmix.Domain.Attendance.FaceVerification;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.Overtime;
using Cosmix.Domain.Attendance.Offices;
using Cosmix.Domain.Attendance.SiteReports;
using Cosmix.Domain.Attendance.WorkSessions;
using Cosmix.Domain.Attendance.Zones;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Files;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.Common.Jobs;
using Cosmix.Domain.Common.Notifications;
using Cosmix.Domain.Common.Reports;
using Cosmix.Domain.HR.AdvancesLoans;
using Cosmix.Domain.HR.Allowances;
using Cosmix.Domain.HR.Compensation;
using Cosmix.Domain.HR.CompanySettings;
using Cosmix.Domain.HR.EmployeeRecords;
using Cosmix.Domain.HR.Holidays;
using Cosmix.Domain.HR.Insurance;
using Cosmix.Domain.HR.Leaves;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.Policies;
using Cosmix.Domain.HR.PolicyEffects;
using Cosmix.Domain.HR.VehicleFuel;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Application.Common.Interfaces;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Onboarding;
using Cosmix.Domain.Attendance.Punches;
using Cosmix.Domain.Attendance.Rosters;
using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.HR.Branches;
using Cosmix.Domain.HR.Departments;
using Cosmix.Domain.HR.Designations;
using Cosmix.Domain.HR.EmployeeAssignments;
using Cosmix.Domain.HR.EmployeeDocuments;
using Cosmix.Domain.HR.Employees;
using Cosmix.Domain.HR.EmploymentTypes;
using Cosmix.Domain.HR.StaffCategories;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Database;

public sealed class CosmixDbContext(DbContextOptions<CosmixDbContext> options)
    : DbContext(options), IUnitOfWork
{
    // Attendance model sets.
    public DbSet<AttendanceCorrection> AttendanceCorrections => Set<AttendanceCorrection>();
    public DbSet<AttendanceCarryForwardAdjustment> AttendanceCarryForwardAdjustments => Set<AttendanceCarryForwardAdjustment>();
    public DbSet<AttendanceException> AttendanceExceptions => Set<AttendanceException>();
    public DbSet<AttendanceCalculationLine> AttendanceCalculationLines => Set<AttendanceCalculationLine>();
    public DbSet<AttendanceSummarySession> AttendanceSummarySessions => Set<AttendanceSummarySession>();
    public DbSet<DailyAttendanceSummary> DailyAttendanceSummaries => Set<DailyAttendanceSummary>();
    public DbSet<AttendancePayrollInputRevision> AttendancePayrollInputRevisions => Set<AttendancePayrollInputRevision>();
    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();
    public DbSet<CompanySiteJurisdictionVersion> CompanySiteJurisdictionVersions => Set<CompanySiteJurisdictionVersion>();
    public DbSet<FaceEnrollment> FaceEnrollments => Set<FaceEnrollment>();
    public DbSet<FaceVerificationAttempt> FaceVerificationAttempts => Set<FaceVerificationAttempt>();
    public DbSet<FaceVerificationException> FaceVerificationExceptions => Set<FaceVerificationException>();
    public DbSet<AttendancePeriodLock> AttendancePeriodLocks => Set<AttendancePeriodLock>();
    public DbSet<AttendancePeriodLockEvent> AttendancePeriodLockEvents => Set<AttendancePeriodLockEvent>();
    public DbSet<AttendancePeriodSummary> AttendancePeriodSummaries => Set<AttendancePeriodSummary>();
    public DbSet<OvertimeRequest> OvertimeRequests => Set<OvertimeRequest>();
    public DbSet<OvertimeEligibilityVersion> OvertimeEligibilityVersions => Set<OvertimeEligibilityVersion>();
    public DbSet<PunchAssessment> PunchAssessments => Set<PunchAssessment>();
    public DbSet<CaptureValidationPolicyVersion> CaptureValidationPolicyVersions => Set<CaptureValidationPolicyVersion>();
    public DbSet<CaptureValidationFinalEvidence> CaptureValidationFinalEvidences => Set<CaptureValidationFinalEvidence>();
    public DbSet<RawPunchEvent> RawPunchEvents => Set<RawPunchEvent>();
    public DbSet<EmployeeShiftAssignment> EmployeeShiftAssignments => Set<EmployeeShiftAssignment>();
    public DbSet<OfficeRosterAssignment> OfficeRosterAssignments => Set<OfficeRosterAssignment>();
    public DbSet<OfficeRosterAssignmentEnd> OfficeRosterAssignmentEnds => Set<OfficeRosterAssignmentEnd>();
    public DbSet<OfficeGeofenceVersion> OfficeGeofenceVersions => Set<OfficeGeofenceVersion>();
    public DbSet<ShiftPopulationAssignment> ShiftPopulationAssignments => Set<ShiftPopulationAssignment>();
    public DbSet<ShiftPopulationAssignmentEnd> ShiftPopulationAssignmentEnds => Set<ShiftPopulationAssignmentEnd>();
    public DbSet<TemporaryShiftChange> TemporaryShiftChanges => Set<TemporaryShiftChange>();
    public DbSet<TemporaryShiftChangeEmployee> TemporaryShiftChangeEmployees => Set<TemporaryShiftChangeEmployee>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftVersion> ShiftVersions => Set<ShiftVersion>();
    public DbSet<SiteReport> SiteReports => Set<SiteReport>();
    public DbSet<SiteReportReview> SiteReportReviews => Set<SiteReportReview>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<SiteGeofenceVersion> SiteGeofenceVersions => Set<SiteGeofenceVersion>();
    public DbSet<WorkSession> WorkSessions => Set<WorkSession>();
    public DbSet<DutyMonitoringWindow> DutyMonitoringWindows => Set<DutyMonitoringWindow>();
    public DbSet<OperationalZone> OperationalZones => Set<OperationalZone>();
    public DbSet<ZoneAssignment> ZoneAssignments => Set<ZoneAssignment>();
    public DbSet<ZoneEvent> ZoneEvents => Set<ZoneEvent>();
    public DbSet<ZoneEventResponse> ZoneEventResponses => Set<ZoneEventResponse>();
    public DbSet<ZoneExceptionRequest> ZoneExceptionRequests => Set<ZoneExceptionRequest>();
    public DbSet<ZoneType> ZoneTypes => Set<ZoneType>();
    public DbSet<ZoneVersion> ZoneVersions => Set<ZoneVersion>();
    public DbSet<ZoneMonitoringConfigurationVersion> ZoneMonitoringConfigurationVersions => Set<ZoneMonitoringConfigurationVersion>();
    public DbSet<ZoneAssignmentEnd> ZoneAssignmentEnds => Set<ZoneAssignmentEnd>();
    public DbSet<ZoneMonitoringCheckpoint> ZoneMonitoringCheckpoints => Set<ZoneMonitoringCheckpoint>();
    public DbSet<ZoneLocationDeletion> ZoneLocationDeletions => Set<ZoneLocationDeletion>();

    // Authentication model sets.
    public DbSet<AccountSetupInvitation> AccountSetupInvitations => Set<AccountSetupInvitation>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<MfaCredential> MfaCredentials => Set<MfaCredential>();
    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RegisteredDevice> RegisteredDevices => Set<RegisteredDevice>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserAccessScope> UserAccessScopes => Set<UserAccessScope>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    // Common model sets.
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalRouteRule> ApprovalRouteRules => Set<ApprovalRouteRule>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<IdempotencyReceipt> IdempotencyReceipts => Set<IdempotencyReceipt>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ReportExportJob> ReportExportJobs => Set<ReportExportJob>();

    // HR model sets.
    public DbSet<AdvanceLoanAgreement> AdvanceLoanAgreements => Set<AdvanceLoanAgreement>();
    public DbSet<AdvanceLoanRequest> AdvanceLoanRequests => Set<AdvanceLoanRequest>();
    public DbSet<RepaymentLedgerEntry> RepaymentLedgerEntries => Set<RepaymentLedgerEntry>();
    public DbSet<RepaymentScheduleEntry> RepaymentScheduleEntries => Set<RepaymentScheduleEntry>();
    public DbSet<AllowanceClaim> AllowanceClaims => Set<AllowanceClaim>();
    public DbSet<AllowanceClaimSubmission> AllowanceClaimSubmissions => Set<AllowanceClaimSubmission>();
    public DbSet<AllowanceSettlementRecord> AllowanceSettlementRecords => Set<AllowanceSettlementRecord>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<CompensationRecord> CompensationRecords => Set<CompensationRecord>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<EmployeeAssignment> EmployeeAssignments => Set<EmployeeAssignment>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeAssetCustody> EmployeeAssetCustodies => Set<EmployeeAssetCustody>();
    public DbSet<EmployeeBankDetails> EmployeeBankDetails => Set<EmployeeBankDetails>();
    public DbSet<EmployeeCertification> EmployeeCertifications => Set<EmployeeCertification>();
    public DbSet<EmployeeMedicalRecord> EmployeeMedicalRecords => Set<EmployeeMedicalRecord>();
    public DbSet<EmployeeSimAssignment> EmployeeSimAssignments => Set<EmployeeSimAssignment>();
    public DbSet<PreviousEmployment> PreviousEmployments => Set<PreviousEmployment>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeAddress> EmployeeAddresses => Set<EmployeeAddress>();
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts => Set<EmployeeEmergencyContact>();
    public DbSet<EmployeeStatusChange> EmployeeStatusChanges => Set<EmployeeStatusChange>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<HolidayCalendar> HolidayCalendars => Set<HolidayCalendar>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();
    public DbSet<InsuranceExpiryReminder> InsuranceExpiryReminders => Set<InsuranceExpiryReminder>();
    public DbSet<LeaveCancellationRequest> LeaveCancellationRequests => Set<LeaveCancellationRequest>();
    public DbSet<LeaveEncashmentRequest> LeaveEncashmentRequests => Set<LeaveEncashmentRequest>();
    public DbSet<LeaveLedgerEntry> LeaveLedgerEntries => Set<LeaveLedgerEntry>();
    public DbSet<LeaveRecoveryCommitment> LeaveRecoveryCommitments => Set<LeaveRecoveryCommitment>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestDay> LeaveRequestDays => Set<LeaveRequestDay>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<EmployeeOnboardingDecision> EmployeeOnboardingDecisions => Set<EmployeeOnboardingDecision>();
    public DbSet<EmployeeSubmission> EmployeeSubmissions => Set<EmployeeSubmission>();
    public DbSet<PayrollHandoffBatch> PayrollHandoffBatches => Set<PayrollHandoffBatch>();
    public DbSet<PayrollHandoffLine> PayrollHandoffLines => Set<PayrollHandoffLine>();
    public DbSet<PayrollHandoffProcessingRecord> PayrollHandoffProcessingRecords => Set<PayrollHandoffProcessingRecord>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<PolicyAssignment> PolicyAssignments => Set<PolicyAssignment>();
    public DbSet<PolicyAssignmentEnd> PolicyAssignmentEnds => Set<PolicyAssignmentEnd>();
    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();
    public DbSet<DisciplinaryCase> DisciplinaryCases => Set<DisciplinaryCase>();
    public DbSet<PolicyImpactAssessment> PolicyImpactAssessments => Set<PolicyImpactAssessment>();
    public DbSet<WaitingTimeEvidence> WaitingTimeEvidences => Set<WaitingTimeEvidence>();
    public DbSet<StaffCategory> StaffCategories => Set<StaffCategory>();
    public DbSet<EmployeeCommuteDistance> EmployeeCommuteDistances => Set<EmployeeCommuteDistance>();
    public DbSet<FuelClaim> FuelClaims => Set<FuelClaim>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleCustody> VehicleCustodies => Set<VehicleCustody>();
    public DbSet<VehicleIncident> VehicleIncidents => Set<VehicleIncident>();
    public DbSet<VehicleMileageEntry> VehicleMileageEntries => Set<VehicleMileageEntry>();
    public DbSet<VehicleServiceRecord> VehicleServiceRecords => Set<VehicleServiceRecord>();
    public DbSet<VehicleTrip> VehicleTrips => Set<VehicleTrip>();

    public DbSet<LocationNoticePolicy> LocationNoticePolicies => Set<LocationNoticePolicy>();
    public DbSet<EmployeeLocationConsent> EmployeeLocationConsents => Set<EmployeeLocationConsent>();
    public DbSet<PunchLocationRedaction> PunchLocationRedactions => Set<PunchLocationRedaction>();
    public DbSet<PayrollObligation> PayrollObligations => Set<PayrollObligation>();
    public DbSet<PayrollJournalEntry> PayrollJournalEntries => Set<PayrollJournalEntry>();
    public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();
    public DbSet<EmployeeStatutoryRegistration> EmployeeStatutoryRegistrations => Set<EmployeeStatutoryRegistration>();
    public DbSet<StatutoryContribution> StatutoryContributions => Set<StatutoryContribution>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("EmployeeNumbers", "hr").StartsAt(1).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CosmixDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges() => SaveChanges(acceptAllChangesOnSuccess: true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTrackedWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateTrackedWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateTrackedWrites()
    {
        // Explicit detection also protects callers that disable AutoDetectChangesEnabled.
        ChangeTracker.DetectChanges();
        ApprovalDelegationWriteGuard.Validate(this);
        EmployeeDocumentWriteGuard.Validate(this);
        EmployeeAssetWriteGuard.Validate(this);
        AttendanceFaceWriteGuard.Validate(this);
        AttendancePeriodWriteGuard.Validate(this);
        LeaveWriteGuard.Validate(this);
        CompanySettingsWriteGuard.Validate(this);
        var ownerEvidenceAdded = ChangeTracker.Entries<EmployeeSubmission>().Any(e => e.State == EntityState.Added &&
            e.Entity.Provenance == OnboardingProvenance.OwnerBootstrap) ||
            ChangeTracker.Entries<EmployeeOnboardingDecision>().Any(e => e.State == EntityState.Added &&
                e.Entity.Provenance == OnboardingProvenance.OwnerBootstrap) ||
            ChangeTracker.Entries<UserAccount>().Any(e => e.State == EntityState.Added &&
                e.Entity.ApprovalDecision?.Provenance == OnboardingProvenance.OwnerBootstrap);
        if (ownerEvidenceAdded && (!Cosmix.Infrastructure.Authentication.Bootstrap.BootstrapWriteScope.IsActive || Database.CurrentTransaction is null))
            throw new InvalidOperationException("First-owner initialization is available only within the local command's guarded transaction.");
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.Entity is IAppendOnlyEntity)
                throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} is append-only; create a new version or an authorized correction.");

            if (entry.Entity is LoginPhoneChange)
            {
                string[] terminal = ["CompletedAt", "CompletionChallengeId", "SupersededAt", "RowVersion"];
                if (entry.State == EntityState.Deleted || entry.Properties.Any(p => p.IsModified && !terminal.Contains(p.Metadata.Name)) ||
                    entry.OriginalValues["CompletedAt"] is not null || entry.OriginalValues["SupersededAt"] is not null)
                    throw new InvalidOperationException("Phone recovery approval and terminal evidence cannot be rewritten or deleted.");
            }
            if (entry.Entity is LoginPhoneBinding)
            {
                string[] lifecycle = ["VerifiedAt", "RevokedAt", "RowVersion"];
                if (entry.State == EntityState.Deleted || entry.Properties.Any(p => p.IsModified && !lifecycle.Contains(p.Metadata.Name)) ||
                    (entry.OriginalValues["VerifiedAt"] is not null && entry.Property("VerifiedAt").IsModified) ||
                    (entry.OriginalValues["RevokedAt"] is not null && entry.Property("RevokedAt").IsModified))
                    throw new InvalidOperationException("Login phone identity and recorded lifecycle evidence cannot be rewritten or deleted.");
            }

            if (entry.Entity is TeamMember or ProjectMember)
            {
                string[] termination = ["TerminatedEffectiveTo", "TerminationReason", "TerminatedByUserId", "TerminatedAt", "TerminationBusinessDate"];
                if (entry.State == EntityState.Deleted || entry.Properties.Any(p => p.IsModified && p.Metadata.Name != "RowVersion" && !termination.Contains(p.Metadata.Name)) ||
                    (entry.OriginalValues["TerminatedEffectiveTo"] is not null && entry.Properties.Any(p => p.IsModified && termination.Contains(p.Metadata.Name))))
                    throw new InvalidOperationException("Membership schedule and recorded termination evidence cannot be rewritten or deleted.");
            }
        }

        foreach (var account in ChangeTracker.Entries<UserAccount>().Where(e => e.State == EntityState.Modified &&
            e.Property(x => x.Status).OriginalValue == AccountStatus.Invited))
        {
            if (!account.Property(x => x.InitialRoleAssignmentId).IsModified && !account.Property(x => x.InitialAccessScopeId).IsModified &&
                !(account.Property(x => x.CurrentSetupInvitationId).IsModified && account.Entity.CurrentSetupInvitationId is null)) continue;
            var oldId = account.Property(x => x.CurrentSetupInvitationId).OriginalValue;
            var old = ChangeTracker.Entries<AccountSetupInvitation>().SingleOrDefault(e => e.Entity.Id == oldId);
            if (old is null || old.State is EntityState.Deleted or EntityState.Added || old.Entity.UserAccountId != account.Entity.Id ||
                old.Entity.ApprovalDecisionId != account.Entity.ApprovalDecisionId || old.Entity.ConsumedAt.HasValue || !old.Entity.RevokedAt.HasValue)
                throw new InvalidOperationException("Recover initial access with its tracked, revoked original invitation in the same transaction.");
        }

        var claims = ChangeTracker.Entries<AllowanceClaim>().ToDictionary(e => e.Entity.Id);
        var submissions = ChangeTracker.Entries<AllowanceClaimSubmission>().Where(e => e.State == EntityState.Added).ToArray();
        var settlements = ChangeTracker.Entries<AllowanceSettlementRecord>().Where(e => e.State == EntityState.Added).ToArray();
        foreach (var submission in submissions)
        {
            if (!claims.TryGetValue(submission.Entity.AllowanceClaimId, out var claim) || claim.State is not (EntityState.Added or EntityState.Modified) ||
                submission.Entity.Version != claim.Entity.SubmissionVersion || submission.Entity.ApprovalRequestId != claim.Entity.ApprovalRequestId ||
                submission.Entity.Version != (claim.State == EntityState.Added ? 1 : claim.Property(x => x.SubmissionVersion).OriginalValue + 1))
                throw new InvalidOperationException("Save each allowance submission with its new claim revision and approval reference.");
            RequireAllowanceApproval(submission.Entity.ApprovalRequestId, claim.Entity.Id, ApprovalState.Pending);
            if (submission.Entity.Version > 1)
                RequireAllowanceApproval(claim.Property(x => x.ApprovalRequestId).OriginalValue!.Value, claim.Entity.Id, ApprovalState.Returned);
        }
        foreach (var settlement in settlements)
        {
            if (!claims.TryGetValue(settlement.Entity.AllowanceClaimId, out var claim) || claim.State != EntityState.Modified ||
                settlement.Entity.SubmissionVersion != claim.Entity.SubmissionVersion || settlement.Entity.State != claim.Entity.SettlementState ||
                settlement.Entity.RecordedAt != claim.Entity.LastSettledAt || (int)claim.Property(x => x.SettlementState).OriginalValue >= (int)settlement.Entity.State)
                throw new InvalidOperationException("Save settlement evidence with its tracked approved claim and forward payment state.");
            RequireAllowanceApproval(claim.Entity.ApprovalRequestId!.Value, claim.Entity.Id, ApprovalState.Approved);
        }
        foreach (var claim in claims.Values.Where(e => e.State == EntityState.Modified))
        {
            string[] revisable = ["ClaimedAmount", "PolicyVersionId", "CalculationPrivateReference", "EvidencePrivateReference", "PayableDate", "PayrollPeriodStart", "ApprovalRequestId", "SubmittedAt"];
            string[] stateFields = ["SubmissionVersion", "SettlementState", "LastSettledAt", "RowVersion"];
            if (claim.Properties.Any(p => p.IsModified && !revisable.Contains(p.Metadata.Name) && !stateFields.Contains(p.Metadata.Name)) ||
                (claim.Properties.Any(p => p.IsModified && revisable.Contains(p.Metadata.Name)) && !claim.Property(x => x.SubmissionVersion).IsModified))
                throw new InvalidOperationException("Allowance identity is fixed; submitted values may change only through a recorded correction revision.");
            if (claim.Property(x => x.SubmissionVersion).IsModified && submissions.Count(s => s.Entity.AllowanceClaimId == claim.Entity.Id) != 1)
                throw new InvalidOperationException("An allowance revision requires exactly one immutable submission record.");
            if ((claim.Property(x => x.SettlementState).IsModified || claim.Property(x => x.LastSettledAt).IsModified) && settlements.Count(s => s.Entity.AllowanceClaimId == claim.Entity.Id) != 1)
                throw new InvalidOperationException("A payment-state change requires exactly one settlement record.");
        }

        // These children are part of an immutable version's original manifest. Later
        // additions would silently change history even though each child is append-only.
        var newSummaries = ChangeTracker.Entries<DailyAttendanceSummary>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id).ToHashSet();
        if (ChangeTracker.Entries<AttendanceCalculationLine>().Any(e => e.State == EntityState.Added && !newSummaries.Contains(e.Entity.DailyAttendanceSummaryId)) ||
            ChangeTracker.Entries<AttendanceSummarySession>().Any(e => e.State == EntityState.Added && !newSummaries.Contains(e.Entity.DailyAttendanceSummaryId)))
            throw new InvalidOperationException("Summary calculation and source rows must be saved with their new summary revision.");

        var newLocks = ChangeTracker.Entries<AttendancePeriodLockEvent>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id).ToHashSet();
        if (ChangeTracker.Entries<AttendancePeriodSummary>().Any(e => e.State == EntityState.Added && !newLocks.Contains(e.Entity.AttendancePeriodLockEventId)))
            throw new InvalidOperationException("Locked summary membership must be saved with its new lock event.");

        var newRoleAssignments = ChangeTracker.Entries<UserRoleAssignment>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id).ToHashSet();
        if (ChangeTracker.Entries<UserAccessScope>().Any(e => e.State == EntityState.Added && !newRoleAssignments.Contains(e.Entity.UserRoleAssignmentId)))
            throw new InvalidOperationException("Scopes must be saved with their new role assignment; replace an assignment to change its scope.");

        var newTemporaryShiftChanges = ChangeTracker.Entries<TemporaryShiftChange>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id).ToHashSet();
        if (ChangeTracker.Entries<TemporaryShiftChangeEmployee>().Any(e => e.State == EntityState.Added && !newTemporaryShiftChanges.Contains(e.Entity.TemporaryShiftChangeId)))
            throw new InvalidOperationException("Temporary shift employees must be saved with their new approved change; create a new change to amend its affected population.");

        var newAgreements = ChangeTracker.Entries<AdvanceLoanAgreement>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id).ToHashSet();
        if (ChangeTracker.Entries<RepaymentScheduleEntry>().Any(e => e.State == EntityState.Added && !newAgreements.Contains(e.Entity.AdvanceLoanAgreementId)))
            throw new InvalidOperationException("A repayment schedule must be saved with its new agreement.");

        // A loan, trip, fuel or service decision must save its approval history in the same
        // transaction. A tracked subject that is decided (or cancelled) without its one matching
        // decision row leaves an approval write with no audit record. Cancelled requests have no
        // decision row because ApprovalRequest.Cancel records none.
        var approvalSubjectIds = ChangeTracker.Entries<AdvanceLoanRequest>().Select(e => e.Entity.Id)
            .Concat(ChangeTracker.Entries<VehicleTrip>().Select(e => e.Entity.Id))
            .Concat(ChangeTracker.Entries<FuelClaim>().Select(e => e.Entity.Id))
            .Concat(ChangeTracker.Entries<VehicleServiceRecord>().Select(e => e.Entity.Id))
            .ToHashSet();
        var newDecisions = ChangeTracker.Entries<ApprovalDecision>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray();
        foreach (var approval in ChangeTracker.Entries<ApprovalRequest>().Where(e => e.State == EntityState.Modified &&
            e.Property(x => x.State).IsModified &&
            e.Property(x => x.State).OriginalValue != ApprovalState.Pending &&
            e.Property(x => x.State).OriginalValue != ApprovalState.Cancelled &&
            approvalSubjectIds.Contains(e.Entity.SubjectId)))
        {
            if (newDecisions.Count(d => d.ApprovalRequestId == approval.Entity.Id && d.DecidedAt == approval.Entity.ClosedAt) != 1)
                throw new InvalidOperationException("An approval decision must be saved with exactly one matching decision record.");
        }

        // A child edit must participate in the employee rowversion transaction. Requiring
        // the tracked parent prevents a concurrent submission from racing a child-only save.
        var employeeIds = ChangeTracker.Entries()
            .Where(e => e.Entity is IEmployeeProfileRecord && (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) &&
                !(e.Entity is EmployeeDocument && e.State == EntityState.Modified))
            .Select(e => ((IEmployeeProfileRecord)e.Entity).EmployeeId).Distinct().ToArray();
        var employees = ChangeTracker.Entries<Employee>().ToDictionary(e => e.Entity.Id);
        foreach (var employeeId in employeeIds)
        {
            if (!employees.TryGetValue(employeeId, out var employee))
                throw new InvalidOperationException("Load the employee aggregate before saving its profile records.");
            var wasPending = employee.State != EntityState.Added &&
                employee.Property(x => x.OnboardingStatus).OriginalValue == OnboardingStatus.PendingApproval;
            if (employee.State == EntityState.Deleted || wasPending ||
                employee.Entity.OnboardingStatus == OnboardingStatus.PendingApproval)
                throw new InvalidOperationException("A submitted employee form cannot be changed while awaiting review.");
        }
        var batchIds = ChangeTracker.Entries<PayrollHandoffLine>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.PayrollHandoffBatchId).Distinct().ToArray();
        var batches = ChangeTracker.Entries<PayrollHandoffBatch>().ToDictionary(e => e.Entity.Id);
        foreach (var batchId in batchIds)
        {
            if (!batches.TryGetValue(batchId, out var batch) || batch.State == EntityState.Deleted)
                throw new InvalidOperationException("Load the payroll handoff batch before adding its lines.");
            if (batch.Entity.LockedAt.HasValue || batch.Property(x => x.LockedAt).OriginalValue.HasValue)
                throw new InvalidOperationException("A locked payroll handoff cannot receive additional lines.");
        }
        foreach (var employeeId in employeeIds)
            employees[employeeId].Entity.RecordRelatedDraftChange();
        foreach (var batchId in batchIds)
            batches[batchId].Entity.RecordLineChange();
        ChangeTracker.DetectChanges();
    }

    private void RequireAllowanceApproval(Guid approvalId, Guid claimId, ApprovalState requiredState)
    {
        var approval = ChangeTracker.Entries<ApprovalRequest>().SingleOrDefault(e => e.Entity.Id == approvalId);
        if (approval is null || approval.State == EntityState.Deleted || approval.Entity.SubjectId != claimId || approval.Entity.State != requiredState)
            throw new InvalidOperationException("Load and track the matching allowance approval before changing its claim.");
        if (requiredState != ApprovalState.Pending && (approval.State == EntityState.Added || approval.Property(x => x.State).IsModified))
        {
            if (!ChangeTracker.Entries<ApprovalDecision>().Any(d => d.State == EntityState.Added &&
                d.Entity.ApprovalRequestId == approvalId && d.Entity.Decision == requiredState && d.Entity.DecidedAt == approval.Entity.ClosedAt))
                throw new InvalidOperationException("A changed allowance approval requires its immutable decision in the same transaction.");
        }
    }
}
