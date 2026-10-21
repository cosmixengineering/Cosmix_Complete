using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.EmployeeRecords;

/// <summary>Append-only attestation for one exact protected bank revision; revocation preserves its predecessor.</summary>
public sealed class EmployeeBankVerification : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private EmployeeBankVerification() { }

    public EmployeeBankVerification(EmployeeBankDetails bank, EmployeeBankVerification? previous, bool isVerified,
        string recordedByUserId, DateTimeOffset recordedAt, string privateEvidenceReference)
    {
        ArgumentNullException.ThrowIfNull(bank);
        if (previous is not null && (previous.EmployeeId != bank.EmployeeId || previous.EmployeeBankDetailsId != bank.Id))
            throw new ArgumentException("Verification history must belong to the same bank revision.", nameof(previous));
        if (isVerified == (previous?.IsVerified ?? bank.IsVerified))
            throw new InvalidOperationException("Verification must change the current status.");
        if (recordedAt < bank.RecordedAt || (previous is not null && recordedAt <= previous.RecordedAt))
            throw new ArgumentException("Verification history must progress in recorded time.", nameof(recordedAt));
        Id = Guid.NewGuid();
        EmployeeId = bank.EmployeeId;
        EmployeeBankDetailsId = bank.Id;
        Version = checked((previous?.Version ?? 0) + 1);
        PreviousVerificationId = previous?.Id;
        IsVerified = isVerified;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
        PrivateEvidenceReference = ModelGuard.Required(privateEvidenceReference, 500, nameof(privateEvidenceReference));
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid EmployeeBankDetailsId { get; private set; }
    public int Version { get; private set; }
    public Guid? PreviousVerificationId { get; private set; }
    public bool IsVerified { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public string PrivateEvidenceReference { get; private set; } = string.Empty;
}
