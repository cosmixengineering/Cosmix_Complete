namespace NOIR.Domain.Entities.Helpdesk;

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum TicketStatus
{
    Open = 1,
    InProgress = 2,
    Closed = 3
}

/// <summary>
/// Helpdesk Ticket aggregate root (Ported from TicketSupportSystem).
/// </summary>
public class Ticket : TenantAggregateRoot<Guid>
{
    private readonly List<TicketComment> _comments = [];

    private Ticket() : base() { }

    private Ticket(Guid id, string? tenantId) : base(id, tenantId) { }

    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketPriority Priority { get; private set; } = TicketPriority.Medium;
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public Guid UserId { get; private set; }
    public Guid? AssignedToId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<TicketComment> Comments => _comments.AsReadOnly();

    public static Ticket Create(
        string title,
        string description,
        TicketPriority priority,
        Guid userId,
        Guid? assignedToId = null,
        string? tenantId = null)
    {
        return new Ticket(Guid.NewGuid(), tenantId)
        {
            Title = title,
            Description = description,
            Priority = priority,
            Status = TicketStatus.Open,
            UserId = userId,
            AssignedToId = assignedToId
        };
    }

    public void AddComment(string text, Guid userId)
    {
        var comment = new TicketComment(Id, text, userId, TenantId);
        _comments.Add(comment);
    }

    public void Assign(Guid assignedToId)
    {
        AssignedToId = assignedToId;
        Status = TicketStatus.InProgress;
    }

    public void UpdateStatus(TicketStatus status)
    {
        Status = status;
        if (status == TicketStatus.Closed)
        {
            ClosedAt = DateTimeOffset.UtcNow;
        }
    }
}

public class TicketComment : TenantEntity<Guid>
{
    private TicketComment() : base() { }

    internal TicketComment(Guid ticketId, string text, Guid userId, string? tenantId = null)
        : base(Guid.NewGuid(), tenantId)
    {
        TicketId = ticketId;
        Text = text;
        UserId = userId;
    }

    public Guid TicketId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
}
