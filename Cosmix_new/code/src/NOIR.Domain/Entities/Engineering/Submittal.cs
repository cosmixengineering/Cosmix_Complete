namespace NOIR.Domain.Entities.Engineering;

public enum SubmittalStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Approved = 4,
    ApprovedWithComments = 5,
    Rejected = 6
}

/// <summary>
/// Technical Submittal aggregate root (SRS Sec 18).
/// Manages engineering document submissions, equipment datasheets, revisions, and consultant approvals.
/// </summary>
public class Submittal : TenantAggregateRoot<Guid>
{
    private Submittal() : base() { }

    private Submittal(Guid id, string? tenantId) : base(id, tenantId) { }

    public string SubmittalNumber { get; private set; } = string.Empty;
    public Guid ProjectId { get; private set; }
    public Guid? BoqId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? SpecificationSection { get; private set; }
    public int RevisionNumber { get; private set; } = 0;
    public SubmittalStatus Status { get; private set; } = SubmittalStatus.Draft;
    public string? DocumentUrl { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewedBy { get; private set; }
    public string? ReviewComments { get; private set; }

    public static Submittal Create(
        string submittalNumber,
        Guid projectId,
        Guid? boqId,
        string title,
        string? specificationSection,
        string? documentUrl,
        string? tenantId = null)
    {
        return new Submittal(Guid.NewGuid(), tenantId)
        {
            SubmittalNumber = submittalNumber,
            ProjectId = projectId,
            BoqId = boqId,
            Title = title,
            SpecificationSection = specificationSection,
            DocumentUrl = documentUrl,
            RevisionNumber = 0,
            Status = SubmittalStatus.Draft,
            SubmittedAt = DateTimeOffset.UtcNow
        };
    }

    public void Submit()
    {
        Status = SubmittalStatus.Submitted;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    public void Review(SubmittalStatus reviewStatus, string reviewedBy, string? comments)
    {
        Status = reviewStatus;
        ReviewedBy = reviewedBy;
        ReviewComments = comments;
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    public void IncrementRevision(string? newDocumentUrl)
    {
        RevisionNumber++;
        Status = SubmittalStatus.Draft;
        DocumentUrl = newDocumentUrl ?? DocumentUrl;
    }
}
