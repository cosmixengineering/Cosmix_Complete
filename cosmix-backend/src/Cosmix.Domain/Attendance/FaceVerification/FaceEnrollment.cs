using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.FaceVerification;

/// <summary>Private encrypted template reference; never a profile photo or raw camera capture.</summary>
public sealed class FaceEnrollment
{
    private FaceEnrollment() { }
    public FaceEnrollment(Guid employeeId, string encryptedTemplateReference, string encryptionKeyReference,
        string provider, string providerVersion, string enrolledBy, string deviceReference,
        string reason, DateTimeOffset enrolledAtUtc, string correlationId, Guid? replacesEnrollmentId = null)
    {
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        EncryptedTemplateReference = ModelGuard.Required(encryptedTemplateReference, 500, nameof(encryptedTemplateReference));
        EncryptionKeyReference = ModelGuard.Required(encryptionKeyReference, 200, nameof(encryptionKeyReference));
        Provider = ModelGuard.Required(provider, 100, nameof(provider));
        ProviderVersion = ModelGuard.Required(providerVersion, 100, nameof(providerVersion));
        EnrolledBy = ModelGuard.Required(enrolledBy, 200, nameof(enrolledBy));
        DeviceReference = ModelGuard.Required(deviceReference, 200, nameof(deviceReference));
        Reason = ModelGuard.Required(reason, 1000, nameof(reason));
        CorrelationId = ModelGuard.Required(correlationId, 100, nameof(correlationId));
        EnrolledAtUtc = ModelGuard.Utc(enrolledAtUtc);
        ReplacesEnrollmentId = replacesEnrollmentId.HasValue ? ModelGuard.Id(replacesEnrollmentId.Value, nameof(replacesEnrollmentId)) : null;
        IsActive = true;
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid? ReplacesEnrollmentId { get; private set; }
    public string EncryptedTemplateReference { get; private set; } = string.Empty;
    public string EncryptionKeyReference { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string ProviderVersion { get; private set; } = string.Empty;
    public string EnrolledBy { get; private set; } = string.Empty;
    public string DeviceReference { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset EnrolledAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public string? RevokedBy { get; private set; }
    public string? RevocationReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Revoke(string actor, string reason, DateTimeOffset atUtc)
    {
        if (!IsActive || atUtc < EnrolledAtUtc) throw new InvalidOperationException("Enrollment is already revoked or revocation predates enrollment.");
        RevokedBy = ModelGuard.Required(actor, 200, nameof(actor));
        RevocationReason = ModelGuard.Required(reason, 1000, nameof(reason));
        RevokedAtUtc = ModelGuard.Utc(atUtc); IsActive = false;
    }
}
