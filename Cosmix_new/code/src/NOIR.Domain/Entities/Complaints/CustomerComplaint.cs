namespace NOIR.Domain.Entities.Complaints;

/// <summary>
/// Customer complaint aggregate root for after-sales services.
/// Links customer reports to projects, equipment, priority levels, and resolution staff.
/// </summary>
public class CustomerComplaint : TenantAggregateRoot<Guid>
{
    private CustomerComplaint() : base() { }

    private CustomerComplaint(Guid id, string? tenantId) : base(id, tenantId) { }

    public string ComplaintNumber { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? EquipmentDetails { get; private set; }
    public string? SiteLocation { get; private set; }
    public ComplaintPriority Priority { get; private set; } = ComplaintPriority.Medium;
    public ComplaintStatus Status { get; private set; } = ComplaintStatus.New;
    public Guid? AssignedTechnicianId { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static CustomerComplaint Create(
        string complaintNumber,
        string subject,
        string description,
        Guid? customerId,
        Guid? projectId,
        string? equipmentDetails,
        string? siteLocation,
        ComplaintPriority priority,
        Guid? assignedTechnicianId,
        string? tenantId = null)
    {
        return new CustomerComplaint(Guid.NewGuid(), tenantId)
        {
            ComplaintNumber = complaintNumber,
            Subject = subject,
            Description = description,
            CustomerId = customerId,
            ProjectId = projectId,
            EquipmentDetails = equipmentDetails,
            SiteLocation = siteLocation,
            Priority = priority,
            Status = assignedTechnicianId.HasValue ? ComplaintStatus.Assigned : ComplaintStatus.New,
            AssignedTechnicianId = assignedTechnicianId
        };
    }

    public void AssignTechnician(Guid technicianId)
    {
        AssignedTechnicianId = technicianId;
        Status = ComplaintStatus.Assigned;
    }

    public void Resolve(string resolutionNotes)
    {
        ResolutionNotes = resolutionNotes;
        Status = ComplaintStatus.Resolved;
        ResolvedAt = DateTimeOffset.UtcNow;
    }

    public void Close()
    {
        Status = ComplaintStatus.Closed;
    }
}
