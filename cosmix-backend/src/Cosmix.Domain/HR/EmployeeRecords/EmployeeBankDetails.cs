using Cosmix.Domain.Common;
using Cosmix.Domain.HR.Employees;

namespace Cosmix.Domain.HR.EmployeeRecords;

/// <summary>Protected bank details and verification are append-only revisions. A successor explicitly replaces its predecessor without rewriting the earlier evidence.</summary>
public sealed class EmployeeBankDetails : IAppendOnlyEntity, IEmployeeProfileRecord
{
    private EmployeeBankDetails() { }

    public EmployeeBankDetails(Guid employeeId, int payloadSchemaVersion, string bankNameEncryptedReference, string accountTitleEncryptedReference, string accountNumberEncryptedReference, string? ibanEncryptedReference, string encryptionKeyReference, string maskedAccountIdentifier, bool isVerified, string? verifiedByUserId, DateTimeOffset? verifiedAt, DateOnly effectiveFrom, DateOnly? effectiveTo, string recordedByUserId, DateTimeOffset recordedAt, int revision = 1, Guid? supersedesEmployeeBankDetailsId = null)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        if (isVerified != (verifiedAt.HasValue && !string.IsNullOrWhiteSpace(verifiedByUserId)) || (!isVerified && (verifiedAt.HasValue || verifiedByUserId != null))) throw new ArgumentException("Verification requires both an actor and timestamp.");
        Id = Guid.NewGuid();
        EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        Revision = HrModelGuard.Positive(revision, nameof(revision));
        if ((revision == 1 && supersedesEmployeeBankDetailsId.HasValue) || (revision > 1 && !supersedesEmployeeBankDetailsId.HasValue))
            throw new ArgumentException("An initial revision has no predecessor; later revisions require one.", nameof(supersedesEmployeeBankDetailsId));
        SupersedesEmployeeBankDetailsId = supersedesEmployeeBankDetailsId.HasValue
            ? ModelGuard.Id(supersedesEmployeeBankDetailsId.Value, nameof(supersedesEmployeeBankDetailsId)) : null;
        PayloadSchemaVersion = HrModelGuard.Positive(payloadSchemaVersion, nameof(payloadSchemaVersion));
        BankNameEncryptedReference = ModelGuard.Required(bankNameEncryptedReference, 500, nameof(bankNameEncryptedReference));
        AccountTitleEncryptedReference = ModelGuard.Required(accountTitleEncryptedReference, 500, nameof(accountTitleEncryptedReference));
        AccountNumberEncryptedReference = ModelGuard.Required(accountNumberEncryptedReference, 500, nameof(accountNumberEncryptedReference));
        IbanEncryptedReference = ModelGuard.Optional(ibanEncryptedReference, 500, nameof(ibanEncryptedReference));
        EncryptionKeyReference = ModelGuard.Required(encryptionKeyReference, 200, nameof(encryptionKeyReference));
        MaskedAccountIdentifier = ModelGuard.Required(maskedAccountIdentifier, 40, nameof(maskedAccountIdentifier));
        IsVerified = isVerified;
        VerifiedByUserId = ModelGuard.Optional(verifiedByUserId, 200, nameof(verifiedByUserId));
        VerifiedAt = verifiedAt?.ToUniversalTime();
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        RecordedByUserId = ModelGuard.Required(recordedByUserId, 200, nameof(recordedByUserId));
        RecordedAt = ModelGuard.Utc(recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public int Revision { get; private set; }
    public Guid? SupersedesEmployeeBankDetailsId { get; private set; }
    public int PayloadSchemaVersion { get; private set; }
    public string BankNameEncryptedReference { get; private set; } = string.Empty;
    public string AccountTitleEncryptedReference { get; private set; } = string.Empty;
    public string AccountNumberEncryptedReference { get; private set; } = string.Empty;
    public string? IbanEncryptedReference { get; private set; }
    public string EncryptionKeyReference { get; private set; } = string.Empty;
    public string MaskedAccountIdentifier { get; private set; } = string.Empty;
    public bool IsVerified { get; private set; }
    public string? VerifiedByUserId { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string RecordedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public EmployeeBankDetails? SupersedesEmployeeBankDetails { get; private set; }
}
