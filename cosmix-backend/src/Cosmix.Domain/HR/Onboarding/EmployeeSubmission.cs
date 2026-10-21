using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.Onboarding;

/// <summary>
/// Immutable submitted version. The private encrypted payload must contain the complete form,
/// related addresses/contacts, explicit login phone/mobile request, assignment values and required document versions by value.
/// Application validates completeness, uniqueness, file scans and the manifest before submission.
/// Storage must retain the exact object version; a mutable URL is not a snapshot.
/// </summary>
public sealed class EmployeeSubmission : IAppendOnlyEntity
{
    private EmployeeSubmission() { }
    internal EmployeeSubmission(Guid employeeId, int version, int draftRevision, Guid? submittedByUserId,
        DateTimeOffset submittedAt, string snapshotStorageKey, string snapshotObjectVersion,
        string snapshotHash, string encryptionKeyReference, int snapshotSchemaVersion,
        string? approvedLoginPhoneNumber, bool mobileAccessRequested,
        OnboardingProvenance provenance = OnboardingProvenance.HrWorkflow, string? externalSubmitterReference = null)
    {
        if (version < 1 || draftRevision < 1 || snapshotSchemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(version));
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Version = version; DraftRevision = draftRevision;
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance));
        Provenance = provenance;
        if (provenance == OnboardingProvenance.HrWorkflow)
        {
            SubmittedByUserId = ModelGuard.Id(submittedByUserId ?? Guid.Empty, nameof(submittedByUserId));
            if (externalSubmitterReference is not null) throw new ArgumentException("HR submissions require their authenticated actor.");
        }
        else
        {
            if (submittedByUserId.HasValue || version != 1 || mobileAccessRequested || approvedLoginPhoneNumber is null)
                throw new InvalidOperationException("Owner bootstrap requires first-version web-only enrollment and external provenance.");
            ExternalSubmitterReference = ModelGuard.Required(externalSubmitterReference!, 200, nameof(externalSubmitterReference));
        }
        SubmittedAt = ModelGuard.Utc(submittedAt);
        SnapshotStorageKey = ModelGuard.Required(snapshotStorageKey, 500, nameof(snapshotStorageKey));
        SnapshotObjectVersion = ModelGuard.Required(snapshotObjectVersion, 200, nameof(snapshotObjectVersion));
        SnapshotHash = ModelGuard.Required(snapshotHash, 128, nameof(snapshotHash));
        EncryptionKeyReference = ModelGuard.Required(encryptionKeyReference, 200, nameof(encryptionKeyReference));
        SnapshotSchemaVersion = snapshotSchemaVersion;
        ApprovedLoginPhoneNumber = mobileAccessRequested || approvedLoginPhoneNumber is not null
            ? LoginPhoneNumber.RequireCanonical(approvedLoginPhoneNumber!, nameof(approvedLoginPhoneNumber)) : null;
        MobileAccessRequested = mobileAccessRequested;
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public int Version { get; private set; }
    public int DraftRevision { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public OnboardingProvenance Provenance { get; private set; }
    public string? ExternalSubmitterReference { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public string SnapshotStorageKey { get; private set; } = string.Empty;
    public string SnapshotObjectVersion { get; private set; } = string.Empty;
    public string SnapshotHash { get; private set; } = string.Empty;
    public string EncryptionKeyReference { get; private set; } = string.Empty;
    public int SnapshotSchemaVersion { get; private set; }
    public string? ApprovedLoginPhoneNumber { get; private set; }
    public bool MobileAccessRequested { get; private set; }
}
