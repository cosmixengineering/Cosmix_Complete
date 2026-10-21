using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Onboarding;

namespace Cosmix.Domain.HR.Employees;

public sealed class Employee
{
    private Employee() { }

    public Employee(DateTimeOffset createdAt, string createdByUserId)
    {
        Id = Guid.NewGuid();
        Status = EmployeeStatus.Draft;
        CreatedAt = ModelGuard.Utc(createdAt);
        CreatedByUserId = ModelGuard.Required(createdByUserId, 200, nameof(createdByUserId));
    }

    public Guid Id { get; private set; }
    // SQL Server assigns this immutable number when the employee is first saved.
    public long EmployeeNumber { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public Guid? LastAppliedStatusChangeId { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public OnboardingStatus OnboardingStatus { get; private set; }
    public int DraftRevision { get; private set; } = 1;
    public int SubmittedVersion { get; private set; }
    public Guid? ApprovedOnboardingDecisionId { get; private set; }
    public string? FullLegalName { get; private set; }
    public string? FatherOrSpouseName { get; private set; }
    public IdentityDocumentType? IdentityDocumentType { get; private set; }
    public string? IdentityDocumentNumber { get; private set; }
    public string? NormalizedIdentityDocumentNumber { get; private set; }
    public DateOnly? IdentityDocumentExpiresOn { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public string? MaritalStatus { get; private set; }
    public string? PersonalPhone { get; private set; }
    public string? CompanyPhone { get; private set; }
    public string? ApprovedLoginPhoneNumber { get; private set; }
    public bool MobileAccessRequested { get; private set; }
    public DateOnly? SimIssuedOn { get; private set; }
    public string? SimStatus { get; private set; }
    public string? SimCreditTier { get; private set; }
    public string? PersonalEmail { get; private set; }
    public string? OfficialEmail { get; private set; }
    public string? NormalizedOfficialEmail { get; private set; }
    public DateOnly? JoinedOn { get; private set; }
    public DateOnly? ProbationEndsOn { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Application verifies authority and the lifecycle approval decision before applying its recorded change.</summary>
    public void ApplyStatusChange(EmployeeStatusChange change, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (OnboardingStatus != OnboardingStatus.Approved ||
            !ApprovedOnboardingDecisionId.HasValue || ApprovedOnboardingDecisionId == Guid.Empty)
            throw new InvalidOperationException("Only an employee with approved onboarding can change employment status.");
        if (change.EmployeeId != Id)
            throw new InvalidOperationException("The status change must belong to this employee.");
        if (!change.ApprovalRequestId.HasValue || change.ApprovalRequestId == Guid.Empty)
            throw new InvalidOperationException("A lifecycle status change requires an approval request.");
        if (change.EffectiveAt > now || change.EffectiveAt < change.RecordedAt)
            throw new InvalidOperationException("The status change must be due and cannot take effect before it was recorded.");
        if (change.PreviousStatus != Status)
            throw new InvalidOperationException("The status change must match the employee's current status.");
        var supported = Status switch
        {
            EmployeeStatus.Active => change.NewStatus is EmployeeStatus.Suspended or EmployeeStatus.Resigned or EmployeeStatus.Terminated ||
                (change.NewStatus == EmployeeStatus.Retired && change.LifecyclePolicyVersion is not null),
            EmployeeStatus.Suspended => change.NewStatus is EmployeeStatus.Resigned or EmployeeStatus.Terminated ||
                (change.NewStatus is EmployeeStatus.Retired or EmployeeStatus.Active && change.LifecyclePolicyVersion is not null && !IsArchived),
            _ => false
        };
        if (!supported)
            throw new InvalidOperationException("This employment status transition is not supported.");

        Status = change.NewStatus;
        LastAppliedStatusChangeId = change.Id;
    }

    /// <summary>Logical directory archival only. Unarchival never changes employment or access.</summary>
    public EmployeeArchiveChange SetArchived(bool archived, string reason, string actorUserId, string actorRoleCode, DateTimeOffset at)
    {
        if (OnboardingStatus != OnboardingStatus.Approved || !ApprovedOnboardingDecisionId.HasValue ||
            Status is not (EmployeeStatus.Resigned or EmployeeStatus.Terminated or EmployeeStatus.Retired))
            throw new InvalidOperationException("Only approved, ended employment can be archived or unarchived.");
        if (IsArchived == archived || at < CreatedAt || at < ArchivedAt)
            throw new InvalidOperationException("The archive state or date conflicts with current history.");
        var change = new EmployeeArchiveChange(Id, IsArchived, archived, Status, reason, actorUserId, actorRoleCode, at);
        IsArchived = archived;
        ArchivedAt = archived ? change.RecordedAt : null;
        return change;
    }

    /// <summary>Explicit draft selection; changing ordinary contacts never changes this credential request.</summary>
    public void SelectLoginPhone(string? phoneNumber, bool mobileAccessRequested)
    {
        if (Status != EmployeeStatus.Draft || OnboardingStatus is not (OnboardingStatus.Draft or OnboardingStatus.Returned))
            throw new InvalidOperationException("Login phone can only be selected on an editable draft.");
        var phone = mobileAccessRequested || phoneNumber is not null
            ? LoginPhoneNumber.RequireCanonical(phoneNumber!, nameof(phoneNumber)) : null;
        var revision = checked(DraftRevision + 1);
        ApprovedLoginPhoneNumber = phone; MobileAccessRequested = mobileAccessRequested; DraftRevision = revision;
    }

    public void UpdateDraft(EmployeeDraftDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (Status != EmployeeStatus.Draft || OnboardingStatus is not (OnboardingStatus.Draft or OnboardingStatus.Returned))
            throw new InvalidOperationException("Only a draft can be edited with this operation.");
        if (details.IdentityDocumentType.HasValue && !Enum.IsDefined(details.IdentityDocumentType.Value))
            throw new ArgumentOutOfRangeException(nameof(details));
        if (details.JoinedOn.HasValue && details.ProbationEndsOn < details.JoinedOn)
            throw new ArgumentException("Probation cannot end before joining.", nameof(details));

        // Validate the entire change before mutating the tracked entity.
        var legalName = ModelGuard.Optional(details.FullLegalName, 200, nameof(details.FullLegalName));
        var parentName = ModelGuard.Optional(details.FatherOrSpouseName, 200, nameof(details.FatherOrSpouseName));
        var documentNumber = ModelGuard.Optional(details.IdentityDocumentNumber, 50, nameof(details.IdentityDocumentNumber));
        var normalizedDocument = documentNumber is null ? null
            : string.Concat(documentNumber.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
        if (normalizedDocument is not null && normalizedDocument.Length == 0)
            throw new ArgumentException("Identity document number must contain a value.", nameof(details));
        var gender = ModelGuard.Optional(details.Gender, 40, nameof(details.Gender));
        var maritalStatus = ModelGuard.Optional(details.MaritalStatus, 40, nameof(details.MaritalStatus));
        var personalPhone = ContactPhone(details.PersonalPhone, nameof(details.PersonalPhone));
        var companyPhone = ContactPhone(details.CompanyPhone, nameof(details.CompanyPhone));
        var simStatus = ModelGuard.Optional(details.SimStatus, 40, nameof(details.SimStatus));
        var simCreditTier = ModelGuard.Optional(details.SimCreditTier, 50, nameof(details.SimCreditTier));
        var personalEmail = ModelGuard.Optional(details.PersonalEmail, 320, nameof(details.PersonalEmail));
        var officialEmail = ModelGuard.Optional(details.OfficialEmail, 320, nameof(details.OfficialEmail));

        FullLegalName = legalName;
        FatherOrSpouseName = parentName;
        IdentityDocumentType = details.IdentityDocumentType;
        IdentityDocumentNumber = documentNumber;
        NormalizedIdentityDocumentNumber = normalizedDocument;
        IdentityDocumentExpiresOn = details.IdentityDocumentExpiresOn;
        DateOfBirth = details.DateOfBirth;
        Gender = gender;
        MaritalStatus = maritalStatus;
        PersonalPhone = personalPhone;
        CompanyPhone = companyPhone;
        SimIssuedOn = details.SimIssuedOn;
        SimStatus = simStatus;
        SimCreditTier = simCreditTier;
        PersonalEmail = personalEmail;
        OfficialEmail = officialEmail;
        NormalizedOfficialEmail = officialEmail?.ToUpperInvariant();
        JoinedOn = details.JoinedOn;
        ProbationEndsOn = details.ProbationEndsOn;
        DraftRevision = checked(DraftRevision + 1);
    }

    /// <summary>Application verifies HR authority and records private before/after history in the same transaction.</summary>
    public void UpdateApprovedBasic(EmployeeBasicDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        RequireApprovedActiveEmployment();
        var personalEmail = ModelGuard.Optional(details.PersonalEmail, 320, nameof(details.PersonalEmail));
        var maritalStatus = ModelGuard.Optional(details.MaritalStatus, 40, nameof(details.MaritalStatus));

        PersonalEmail = personalEmail;
        MaritalStatus = maritalStatus;
    }

    /// <summary>Application verifies the distinct fixed Admin approval and records private history before committing.</summary>
    public void UpdateApprovedIdentity(EmployeeIdentityDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        RequireApprovedActiveEmployment();
        if (!Enum.IsDefined(details.IdentityDocumentType))
            throw new ArgumentOutOfRangeException(nameof(details));

        // Match draft normalization, validating every field before changing the approved profile.
        var legalName = ModelGuard.Required(details.FullLegalName, 200, nameof(details.FullLegalName));
        var parentName = ModelGuard.Optional(details.FatherOrSpouseName, 200, nameof(details.FatherOrSpouseName));
        var documentNumber = ModelGuard.Required(details.IdentityDocumentNumber, 50, nameof(details.IdentityDocumentNumber));
        var normalizedDocument = string.Concat(documentNumber.Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
        if (normalizedDocument.Length == 0)
            throw new ArgumentException("Identity document number must contain a value.", nameof(details));
        var gender = ModelGuard.Optional(details.Gender, 40, nameof(details.Gender));
        var personalPhone = LoginPhoneNumber.RequireCanonical(details.PersonalPhone, nameof(details.PersonalPhone));
        var companyPhone = ContactPhone(details.CompanyPhone, nameof(details.CompanyPhone));
        var officialEmail = ModelGuard.Optional(details.OfficialEmail, 320, nameof(details.OfficialEmail));
        var normalizedOfficialEmail = officialEmail?.ToUpperInvariant();

        FullLegalName = legalName;
        FatherOrSpouseName = parentName;
        IdentityDocumentType = details.IdentityDocumentType;
        IdentityDocumentNumber = documentNumber;
        NormalizedIdentityDocumentNumber = normalizedDocument;
        IdentityDocumentExpiresOn = details.IdentityDocumentExpiresOn;
        DateOfBirth = details.DateOfBirth;
        Gender = gender;
        PersonalPhone = personalPhone;
        CompanyPhone = companyPhone;
        OfficialEmail = officialEmail;
        NormalizedOfficialEmail = normalizedOfficialEmail;
    }

    private void RequireApprovedActiveEmployment()
    {
        if (Status != EmployeeStatus.Active || OnboardingStatus != OnboardingStatus.Approved ||
            !ApprovedOnboardingDecisionId.HasValue || ApprovedOnboardingDecisionId == Guid.Empty)
            throw new InvalidOperationException("Only an active employee with an approved onboarding decision can be updated.");
    }

    private static string? ContactPhone(string? value, string name) => string.IsNullOrWhiteSpace(value)
        ? null : LoginPhoneNumber.RequireCanonical(value, name);

    /// <summary>Called by persistence for tracked aggregate children to lock against a concurrent submission.</summary>
    public void RecordRelatedDraftChange()
    {
        if (OnboardingStatus == OnboardingStatus.PendingApproval)
            throw new InvalidOperationException("The submitted employee form cannot change while awaiting review.");
        DraftRevision = checked(DraftRevision + 1);
    }

    public EmployeeSubmission SubmitForApproval(Guid submittedByUserId, DateTimeOffset submittedAt,
        string snapshotStorageKey, string snapshotObjectVersion, string snapshotHash,
        string encryptionKeyReference, int snapshotSchemaVersion)
    {
        ValidateSubmissionDraft();
        var submission = new EmployeeSubmission(Id, checked(SubmittedVersion + 1), DraftRevision,
            submittedByUserId, submittedAt, snapshotStorageKey, snapshotObjectVersion, snapshotHash,
            encryptionKeyReference, snapshotSchemaVersion, ApprovedLoginPhoneNumber, MobileAccessRequested);
        SubmittedVersion = submission.Version; OnboardingStatus = OnboardingStatus.PendingApproval;
        Status = EmployeeStatus.PendingApproval;
        return submission;
    }

    /// <summary>Local owner-authorized initialization only; persistence must enforce the singleton and empty database.</summary>
    public EmployeeSubmission SubmitForOwnerBootstrap(string externalSubmitterReference, DateTimeOffset submittedAt,
        string snapshotStorageKey, string snapshotObjectVersion, string snapshotHash,
        string encryptionKeyReference, int snapshotSchemaVersion)
    {
        ValidateSubmissionDraft();
        if (SubmittedVersion != 0 || OnboardingStatus != OnboardingStatus.Draft || MobileAccessRequested ||
            ApprovedLoginPhoneNumber is null || submittedAt < CreatedAt)
            throw new InvalidOperationException("Owner bootstrap requires a new web-only identity.");
        var submission = new EmployeeSubmission(Id, 1, DraftRevision, null, submittedAt,
            snapshotStorageKey, snapshotObjectVersion, snapshotHash, encryptionKeyReference, snapshotSchemaVersion,
            ApprovedLoginPhoneNumber, false, OnboardingProvenance.OwnerBootstrap, externalSubmitterReference);
        SubmittedVersion = 1; OnboardingStatus = OnboardingStatus.PendingApproval; Status = EmployeeStatus.PendingApproval;
        return submission;
    }

    public EmployeeOnboardingDecision ApproveOwnerBootstrap(EmployeeSubmission submission,
        string externalApproverReference, string ownerApprovalReference, DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (OnboardingStatus != OnboardingStatus.PendingApproval || Status != EmployeeStatus.PendingApproval ||
            submission.EmployeeId != Id || submission.Version != SubmittedVersion || submission.DraftRevision != DraftRevision)
            throw new InvalidOperationException("Only this employee's current bootstrap submission can be approved.");
        var decision = new EmployeeOnboardingDecision(submission, externalApproverReference, ownerApprovalReference, decidedAt);
        OnboardingStatus = OnboardingStatus.Approved; Status = EmployeeStatus.Active; ApprovedOnboardingDecisionId = decision.Id;
        return decision;
    }

    private void ValidateSubmissionDraft()
    {
        if (OnboardingStatus is not (OnboardingStatus.Draft or OnboardingStatus.Returned) || Status != EmployeeStatus.Draft)
            throw new InvalidOperationException("Only a new or returned draft can be submitted.");
        if (string.IsNullOrWhiteSpace(FullLegalName) || !IdentityDocumentType.HasValue ||
            string.IsNullOrWhiteSpace(NormalizedIdentityDocumentNumber) || !JoinedOn.HasValue ||
            (string.IsNullOrWhiteSpace(PersonalPhone) && string.IsNullOrWhiteSpace(CompanyPhone)))
            throw new InvalidOperationException("Identity, name, contact and joining date are required before submission.");
    }

    public EmployeeOnboardingDecision DecideSubmission(EmployeeSubmission submission, OnboardingDecision decision,
        Guid actorUserId, Guid actorRoleId, string? reason, DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (OnboardingStatus != OnboardingStatus.PendingApproval || submission.EmployeeId != Id ||
            submission.Version != SubmittedVersion || submission.DraftRevision != DraftRevision)
            throw new InvalidOperationException("Only the employee's current submitted version can be decided.");
        var result = new EmployeeOnboardingDecision(submission, decision, actorUserId, actorRoleId, reason, decidedAt);
        OnboardingStatus = decision switch
        {
            OnboardingDecision.Approve => OnboardingStatus.Approved,
            OnboardingDecision.ReturnForCorrection => OnboardingStatus.Returned,
            _ => OnboardingStatus.Rejected
        };
        // Employment approval does not create or activate a login account.
        Status = decision == OnboardingDecision.Approve ? EmployeeStatus.Active : EmployeeStatus.Draft;
        if (decision == OnboardingDecision.Approve) ApprovedOnboardingDecisionId = result.Id;
        return result;
    }
}
