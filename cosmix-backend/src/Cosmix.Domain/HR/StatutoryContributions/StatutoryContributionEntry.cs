using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.StatutoryContributions;

public sealed class StatutoryContributionEntry : IAppendOnlyEntity
{
    private StatutoryContributionEntry() { }
    public StatutoryContributionEntry(Guid contributionId, Guid employeeId, Guid registrationId, string registrationNumber,
        decimal employeeAmount, decimal employerAmount, string evidenceReference, string evidenceSha256, string privateProof)
    {
        Id = Guid.NewGuid(); ContributionId = ModelGuard.Id(contributionId, nameof(contributionId)); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId));
        RegistrationId = ModelGuard.Id(registrationId, nameof(registrationId)); RegistrationNumber = ModelGuard.Required(registrationNumber, 100, nameof(registrationNumber));
        EmployeeContributionAmount = HrModelGuard.Money(employeeAmount, nameof(employeeAmount)); EmployerContributionAmount = HrModelGuard.Money(employerAmount, nameof(employerAmount));
        EvidenceReference = ModelGuard.Required(evidenceReference, 300, nameof(evidenceReference)); EvidenceSha256 = ValidateSha256(evidenceSha256);
        PrivateProofReference = ModelGuard.Required(privateProof, 500, nameof(privateProof));
    }
    public Guid Id { get; private set; }
    public Guid ContributionId { get; private set; }
    public StatutoryContribution Contribution { get; private set; } = null!;
    public Guid EmployeeId { get; private set; }
    public Guid RegistrationId { get; private set; }
    public EmployeeStatutoryRegistration Registration { get; private set; } = null!;
    public string RegistrationNumber { get; private set; } = "";
    public decimal EmployeeContributionAmount { get; private set; }
    public decimal EmployerContributionAmount { get; private set; }
    public string EvidenceReference { get; private set; } = "";
    public string EvidenceSha256 { get; private set; } = "";
    public string PrivateProofReference { get; private set; } = "";
    public static string ValidateSha256(string value)
    {
        var normalized = ModelGuard.Required(value, 64, nameof(value)).ToUpperInvariant();
        if (normalized.Length != 64 || !normalized.All(Uri.IsHexDigit)) throw new ArgumentException("SHA256 required.");
        return normalized;
    }
}

public sealed class EmployeeStatutoryCoverage
{
    private EmployeeStatutoryCoverage() { }
    public EmployeeStatutoryCoverage(Guid employeeId, DateOnly from, DateOnly? toExclusive, IEnumerable<StatutoryScheme> schemes,
        string evidenceReference, string evidenceSha256, string privateProof, string maker, DateTimeOffset at)
    {
        if (toExclusive <= from) throw new ArgumentException("Invalid coverage dates.");
        var values = schemes.ToArray();
        if (values.Length > 3 || values.Distinct().Count() != values.Length || values.Any(x => !Enum.IsDefined(x))) throw new ArgumentException("Explicit unique schemes required.");
        Id = Guid.NewGuid(); EmployeeId = ModelGuard.Id(employeeId, nameof(employeeId)); EffectiveFrom = from; EffectiveToExclusive = toExclusive;
        Schemes = string.Join(',', values.Order().Select(x => (int)x)); EvidenceReference = ModelGuard.Required(evidenceReference, 300, nameof(evidenceReference));
        EvidenceSha256 = StatutoryContributionEntry.ValidateSha256(evidenceSha256); PrivateProofReference = ModelGuard.Required(privateProof, 500, nameof(privateProof));
        CreatedByUserId = ModelGuard.Required(maker, 200, nameof(maker)); CreatedAt = ModelGuard.Utc(at);
    }
    public Guid Id { get; private set; }
    public Guid EmployeeId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveToExclusive { get; private set; }
    public string Schemes { get; private set; } = "";
    public string EvidenceReference { get; private set; } = "";
    public string EvidenceSha256 { get; private set; } = "";
    public string PrivateProofReference { get; private set; } = "";
    public string CreatedByUserId { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyList<StatutoryScheme> ReadSchemes() => string.IsNullOrEmpty(Schemes) ? [] : Schemes.Split(',').Select(x => (StatutoryScheme)int.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    public void Approve(string actor, DateTimeOffset at)
    {
        if (ApprovedAt.HasValue || actor == CreatedByUserId) throw new InvalidOperationException("Independent coverage approval required.");
        ApprovedByUserId = ModelGuard.Required(actor, 200, nameof(actor)); ApprovedAt = ModelGuard.Utc(at);
    }
}
