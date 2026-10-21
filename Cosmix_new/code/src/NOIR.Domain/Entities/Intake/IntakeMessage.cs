namespace NOIR.Domain.Entities.Intake;

public enum IntakeChannel
{
    Email = 1,
    Website = 2,
    WhatsApp = 3,
    SMS = 4,
    Manual = 5,
    SocialMedia = 6
}

public enum IntakeStatus
{
    New = 1,
    Assigned = 2,
    InReview = 3,
    Processed = 4,
    Archived = 5
}

/// <summary>
/// Unified Intake message aggregate root (SRS Sec 8).
/// Centralizes inbound inquiries from email, website, and messaging channels while preserving immutable raw message bodies.
/// </summary>
public class IntakeMessage : TenantAggregateRoot<Guid>
{
    private IntakeMessage() : base() { }

    private IntakeMessage(Guid id, string? tenantId) : base(id, tenantId) { }

    public string MessageNumber { get; private set; } = string.Empty;
    public IntakeChannel Channel { get; private set; } = IntakeChannel.Email;
    public string SenderName { get; private set; } = string.Empty;
    public string? SenderEmail { get; private set; }
    public string? SenderPhone { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string RawBody { get; private set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private set; } = DateTimeOffset.UtcNow;
    public IntakeStatus Status { get; private set; } = IntakeStatus.New;
    public Guid? AssignedDepartmentId { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public string? ProcessingNotes { get; private set; }

    public static IntakeMessage Create(
        string messageNumber,
        IntakeChannel channel,
        string senderName,
        string? senderEmail,
        string? senderPhone,
        string subject,
        string rawBody,
        Guid? assignedDepartmentId = null,
        string? tenantId = null)
    {
        return new IntakeMessage(Guid.NewGuid(), tenantId)
        {
            MessageNumber = messageNumber,
            Channel = channel,
            SenderName = senderName,
            SenderEmail = senderEmail,
            SenderPhone = senderPhone,
            Subject = subject,
            RawBody = rawBody,
            ReceivedAt = DateTimeOffset.UtcNow,
            Status = assignedDepartmentId.HasValue ? IntakeStatus.Assigned : IntakeStatus.New,
            AssignedDepartmentId = assignedDepartmentId
        };
    }

    public void Assign(Guid departmentId, Guid? userId = null)
    {
        AssignedDepartmentId = departmentId;
        AssignedUserId = userId;
        Status = IntakeStatus.Assigned;
    }

    public void MarkProcessed(string notes)
    {
        Status = IntakeStatus.Processed;
        ProcessingNotes = notes;
    }

    public void Archive()
    {
        Status = IntakeStatus.Archived;
    }
}
