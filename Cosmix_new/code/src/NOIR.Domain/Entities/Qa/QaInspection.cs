namespace NOIR.Domain.Entities.Qa;

public enum QaStage
{
    BoqVerification = 1,
    EngineeringReview = 2,
    ProcurementReceiving = 3,
    ManufacturingProcess = 4,
    DeliveryDispatch = 5,
    FieldJobInstallation = 6,
    ProjectHandover = 7
}

public enum QaResult
{
    Pending = 1,
    Passed = 2,
    ConditionalPass = 3,
    Failed = 4
}

/// <summary>
/// Quality Assurance Inspection aggregate root (SRS Sec 57).
/// Provides checklists, inspections, and defect sign-offs polymorphic across BOQ, Procurement, Manufacturing, Delivery, and Field Jobs.
/// </summary>
public class QaInspection : TenantAggregateRoot<Guid>
{
    private QaInspection() : base() { }

    private QaInspection(Guid id, string? tenantId) : base(id, tenantId) { }

    public string InspectionNumber { get; private set; } = string.Empty;
    public QaStage Stage { get; private set; } = QaStage.FieldJobInstallation;
    public Guid? ReferenceEntityId { get; private set; }
    public string? ReferenceEntityType { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? ChecklistSummary { get; private set; }
    public QaResult Result { get; private set; } = QaResult.Pending;
    public string? InspectedBy { get; private set; }
    public DateTimeOffset InspectedAt { get; private set; } = DateTimeOffset.UtcNow;
    public string? DefectNotes { get; private set; }
    public string? CorrectiveAction { get; private set; }
    public string? EvidenceDocumentUrl { get; private set; }

    public static QaInspection Create(
        string inspectionNumber,
        QaStage stage,
        Guid? referenceEntityId,
        string? referenceEntityType,
        string title,
        string? checklistSummary,
        string? tenantId = null)
    {
        return new QaInspection(Guid.NewGuid(), tenantId)
        {
            InspectionNumber = inspectionNumber,
            Stage = stage,
            ReferenceEntityId = referenceEntityId,
            ReferenceEntityType = referenceEntityType,
            Title = title,
            ChecklistSummary = checklistSummary,
            Result = QaResult.Pending,
            InspectedAt = DateTimeOffset.UtcNow
        };
    }

    public void RecordResult(
        QaResult result,
        string inspectedBy,
        string? defectNotes = null,
        string? correctiveAction = null,
        string? evidenceDocumentUrl = null)
    {
        Result = result;
        InspectedBy = inspectedBy;
        DefectNotes = defectNotes;
        CorrectiveAction = correctiveAction;
        EvidenceDocumentUrl = evidenceDocumentUrl;
        InspectedAt = DateTimeOffset.UtcNow;
    }
}
