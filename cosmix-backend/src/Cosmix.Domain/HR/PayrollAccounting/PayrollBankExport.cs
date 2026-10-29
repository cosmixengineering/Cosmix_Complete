using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.PayrollAccounting;

/// <summary>A supplied format reviewed by Finance. It is not a bank certification or transfer instruction.</summary>
public sealed class PayrollBankFormatProfile
{
    private PayrollBankFormatProfile() { }
    public PayrollBankFormatProfile(string name, int formatVersion, string currency, string rulesJson, string maker, DateTimeOffset at)
    {
        Id = Guid.NewGuid(); Name = ModelGuard.Required(name, 150, nameof(name)); FormatVersion = HrModelGuard.Positive(formatVersion, nameof(formatVersion));
        Currency = HrModelGuard.Currency(currency); RulesJson = HrModelGuard.JsonObject(rulesJson, 15000, nameof(rulesJson));
        CreatedByUserId = ModelGuard.Required(maker, 200, nameof(maker)); CreatedAt = ModelGuard.Utc(at);
        State = PayrollFinanceApprovalState.Draft;
    }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public int FormatVersion { get; private set; }
    public string Currency { get; private set; } = "";
    public string RulesJson { get; private set; } = "";
    public string? TemplateProofReference { get; private set; }
    public string? ApprovalProofReference { get; private set; }
    public PayrollFinanceApprovalState State { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public int Version { get; private set; } = 1;
    public void BindTemplate(string proof)
    {
        if (TemplateProofReference is not null || State != PayrollFinanceApprovalState.Draft) throw new InvalidOperationException("Format evidence is immutable.");
        TemplateProofReference = ModelGuard.Required(proof, 2000, nameof(proof));
    }
    public void Approve(string actor, DateTimeOffset at, string proof)
    {
        if (State != PayrollFinanceApprovalState.Draft || TemplateProofReference is null || string.Equals(actor, CreatedByUserId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Retained supplied format and independent review are required.");
        var reference = ModelGuard.Required(proof, 2000, nameof(proof));
        ApprovedByUserId = ModelGuard.Required(actor, 200, nameof(actor)); ApprovedAt = ModelGuard.Utc(at);
        ApprovalProofReference = reference; State = PayrollFinanceApprovalState.Approved; Version++;
    }
}

public sealed class PayrollBankExport : IAppendOnlyEntity
{
    private PayrollBankExport() { }
    public PayrollBankExport(Guid obligationId, Guid profileId, DateOnly valueDate, string sourceFingerprint, string createdBy, DateTimeOffset at)
    {
        Id = Guid.NewGuid(); ObligationId = ModelGuard.Id(obligationId, nameof(obligationId)); FormatProfileId = ModelGuard.Id(profileId, nameof(profileId));
        ValueDate = valueDate; SourceFingerprint = ModelGuard.Required(sourceFingerprint, 64, nameof(sourceFingerprint));
        CreatedByUserId = ModelGuard.Required(createdBy, 200, nameof(createdBy)); CreatedAt = ModelGuard.Utc(at); ExpiresAt = CreatedAt.AddHours(24);
    }
    public Guid Id { get; private set; }
    public Guid ObligationId { get; private set; }
    public Guid FormatProfileId { get; private set; }
    public DateOnly ValueDate { get; private set; }
    public string SourceFingerprint { get; private set; } = "";
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string? PrivateFileReference { get; private set; }
    public string? PrivateManifestReference { get; private set; }
    public string? Sha256 { get; private set; }
    public int PaymentCount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public void Complete(string outputReference, string manifestReference, string sha256, int paymentCount, decimal totalAmount)
    {
        if (PrivateFileReference is not null) throw new InvalidOperationException("Bank export output is immutable.");
        PrivateFileReference = ModelGuard.Required(outputReference, 2000, nameof(outputReference));
        PrivateManifestReference = ModelGuard.Required(manifestReference, 2000, nameof(manifestReference));
        Sha256 = ModelGuard.Required(sha256, 64, nameof(sha256)); PaymentCount = HrModelGuard.Positive(paymentCount, nameof(paymentCount));
        TotalAmount = HrModelGuard.Money(totalAmount, nameof(totalAmount));
    }
}

/// <summary>An audited extension of access to the same immutable file; never a new transfer instruction.</summary>
public sealed class PayrollBankExportRenewal : IAppendOnlyEntity
{
    private PayrollBankExportRenewal() { }
    public PayrollBankExportRenewal(Guid exportId, string actor, DateTimeOffset at, string privateProofReference)
    {
        Id = Guid.NewGuid(); ExportId = ModelGuard.Id(exportId, nameof(exportId)); CreatedByUserId = ModelGuard.Required(actor, 200, nameof(actor));
        CreatedAt = ModelGuard.Utc(at); ExpiresAt = CreatedAt.AddHours(24);
        PrivateProofReference = ModelGuard.Required(privateProofReference, 2000, nameof(privateProofReference));
    }
    public Guid Id { get; private set; }
    public Guid ExportId { get; private set; }
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string PrivateProofReference { get; private set; } = "";
}
