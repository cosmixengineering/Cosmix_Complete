namespace NOIR.Domain.Entities.Helpdesk;

public enum HelpdeskCategory
{
    ITSupport = 1,
    HRInquiry = 2,
    Administration = 3,
    Maintenance = 4,
    Finance = 5,
    General = 6
}

public enum HelpdeskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum HelpdeskStatus
{
    Open = 1,
    InProgress = 2,
    Resolved = 3,
    Closed = 4
}

/// <summary>
/// Internal Helpdesk ticket aggregate root for employees (SRS Sec 45).
/// </summary>
public class HelpdeskTicket : TenantAggregateRoot<Guid>
{
    private HelpdeskTicket() : base() { }

    private HelpdeskTicket(Guid id, string? tenantId) : base(id, tenantId) { }

    public string TicketNumber { get; private set; } = string.Empty;
    public Guid EmployeeId { get; private set; }
    public HelpdeskCategory Category { get; private set; } = HelpdeskCategory.ITSupport;
    public HelpdeskPriority Priority { get; private set; } = HelpdeskPriority.Medium;
    public HelpdeskStatus Status { get; private set; } = HelpdeskStatus.Open;
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid? AssignedToEmployeeId { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static HelpdeskTicket Create(
        string ticketNumber,
        Guid employeeId,
        HelpdeskCategory category,
        HelpdeskPriority priority,
        string subject,
        string description,
        string? tenantId = null)
    {
        return new HelpdeskTicket(Guid.NewGuid(), tenantId)
        {
            TicketNumber = ticketNumber,
            EmployeeId = employeeId,
            Category = category,
            Priority = priority,
            Subject = subject,
            Description = description,
            Status = HelpdeskStatus.Open
        };
    }

    public void Assign(Guid assignedToEmployeeId)
    {
        AssignedToEmployeeId = assignedToEmployeeId;
        Status = HelpdeskStatus.InProgress;
    }

    public void Resolve(string resolutionNotes)
    {
        ResolutionNotes = resolutionNotes;
        Status = HelpdeskStatus.Resolved;
        ResolvedAt = DateTimeOffset.UtcNow;
    }

    public void Close()
    {
        Status = HelpdeskStatus.Closed;
    }
}
