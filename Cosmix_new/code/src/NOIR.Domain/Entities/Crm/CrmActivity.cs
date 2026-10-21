namespace NOIR.Domain.Entities.Crm;

/// <summary>
/// An activity (call, email, meeting, note) logged against a contact or lead.
/// </summary>
public class CrmActivity : TenantAggregateRoot<Guid>
{
    public ActivityType Type { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? ContactId { get; private set; }
    public Guid? LeadId { get; private set; }
    public Guid PerformedById { get; private set; }
    public DateTimeOffset PerformedAt { get; private set; }
    public int? DurationMinutes { get; private set; }

    // Navigation properties
    public virtual CrmContact? Contact { get; private set; }
    public virtual Lead? Lead { get; private set; }
    public virtual Employee? PerformedBy { get; private set; }

    // Private constructor for EF Core
    private CrmActivity() : base() { }

    private CrmActivity(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new CRM activity.
    /// </summary>
    public static CrmActivity Create(
        ActivityType type,
        string subject,
        Guid performedById,
        DateTimeOffset performedAt,
        string? tenantId,
        string? description = null,
        Guid? contactId = null,
        Guid? leadId = null,
        int? durationMinutes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        return new CrmActivity(Guid.NewGuid(), tenantId)
        {
            Type = type,
            Subject = subject.Trim(),
            Description = description?.Trim(),
            ContactId = contactId,
            LeadId = leadId,
            PerformedById = performedById,
            PerformedAt = performedAt,
            DurationMinutes = durationMinutes
        };
    }

    /// <summary>
    /// Updates activity details.
    /// </summary>
    public void Update(
        ActivityType type,
        string subject,
        string? description,
        Guid? contactId,
        Guid? leadId,
        DateTimeOffset performedAt,
        int? durationMinutes)
    {
        Type = type;
        Subject = subject.Trim();
        Description = description?.Trim();
        ContactId = contactId;
        LeadId = leadId;
        PerformedAt = performedAt;
        DurationMinutes = durationMinutes;
    }
}
