using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollImports;

public sealed class PayrollSalaryImport
{
    private PayrollSalaryImport() { }
    public PayrollSalaryImport(Guid id, string fileName, string sourceSha256, string sourcePrivateReference,
        string uploadedByUserId, DateTimeOffset uploadedAt)
    {
        Id = ModelGuard.Id(id, nameof(id));
        FileName = ModelGuard.Required(fileName, 150, nameof(fileName));
        SourceSha256 = ModelGuard.Required(sourceSha256, 64, nameof(sourceSha256));
        SourcePrivateReference = ModelGuard.Required(sourcePrivateReference, 4000, nameof(sourcePrivateReference));
        UploadedByUserId = ModelGuard.Required(uploadedByUserId, 200, nameof(uploadedByUserId));
        UploadedAt = ModelGuard.Utc(uploadedAt);
    }
    public Guid Id { get; private set; }
    public string FileName { get; private set; } = "";
    public string SourceSha256 { get; private set; } = "";
    public string SourcePrivateReference { get; private set; } = "";
    public string UploadedByUserId { get; private set; } = "";
    public DateTimeOffset UploadedAt { get; private set; }
    public string? AppliedFingerprint { get; private set; }
    public string? AppliedPrivateReference { get; private set; }
    public string? SubmittedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Submit(string fingerprint, string evidence, string actor, DateTimeOffset now)
    {
        if (SubmittedAt.HasValue || now < UploadedAt) throw new InvalidOperationException("Import can only be submitted once.");
        AppliedFingerprint = ModelGuard.Required(fingerprint, 64, nameof(fingerprint));
        AppliedPrivateReference = ModelGuard.Required(evidence, 4000, nameof(evidence));
        SubmittedByUserId = ModelGuard.Required(actor, 200, nameof(actor)); SubmittedAt = ModelGuard.Utc(now);
    }
}
