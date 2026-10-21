using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.FaceVerification;

/// <summary>Requested accessible alternative; approval resolution and permission checks remain server-side.</summary>
public sealed class FaceVerificationException : IAppendOnlyEntity
{
    private FaceVerificationException() { }
    public FaceVerificationException(Guid employeeId, Guid approvalRequestId, string alternativeMethodCode,
        string privateEvidenceReference, DateOnly effectiveFrom, DateOnly effectiveTo, string requestedBy, DateTimeOffset requestedAtUtc)
    {
        ModelGuard.EffectiveDates(effectiveFrom, effectiveTo);
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        ApprovalRequestId = ModelGuard.Id(approvalRequestId, nameof(approvalRequestId));
        AlternativeMethodCode = ModelGuard.Code(alternativeMethodCode, 100);
        PrivateEvidenceReference = ModelGuard.Required(privateEvidenceReference, 500, nameof(privateEvidenceReference));
        EffectiveFrom = effectiveFrom; EffectiveTo = effectiveTo;
        RequestedBy = ModelGuard.Required(requestedBy, 200, nameof(requestedBy)); RequestedAtUtc = ModelGuard.Utc(requestedAtUtc);
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid ApprovalRequestId { get; private set; }
    public string AlternativeMethodCode { get; private set; } = string.Empty;
    public string PrivateEvidenceReference { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly EffectiveTo { get; private set; }
    public string RequestedBy { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; private set; }
}
