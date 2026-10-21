namespace NOIR.Domain.Entities.Fsm;

/// <summary>
/// Field Service Management job aggregate root.
/// Supports technician assignments, work status tracking, site coordinates, and customer OTP completion.
/// </summary>
public class FieldJob : TenantAggregateRoot<Guid>
{
    private FieldJob() : base() { }

    private FieldJob(Guid id, string? tenantId) : base(id, tenantId) { }

    public string JobNumber { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public Guid? CustomerId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public Guid? TechnicianId { get; private set; }
    public string? EquipmentDetails { get; private set; }
    public JobType Type { get; private set; } = JobType.Installation;
    public JobStatus Status { get; private set; } = JobStatus.Pending;
    public string? SiteAddress { get; private set; }
    public double? SiteLatitude { get; private set; }
    public double? SiteLongitude { get; private set; }
    public DateTimeOffset? ScheduledDate { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CustomerOtp { get; private set; }
    public string? MaterialUsage { get; private set; }
    public string? Notes { get; private set; }

    public static FieldJob Create(
        string jobNumber,
        string title,
        Guid? customerId,
        Guid? projectId,
        Guid? technicianId,
        string? equipmentDetails,
        JobType type,
        string? siteAddress,
        double? siteLatitude,
        double? siteLongitude,
        DateTimeOffset? scheduledDate,
        string? customerOtp,
        string? notes,
        string? tenantId = null)
    {
        var job = new FieldJob(Guid.NewGuid(), tenantId)
        {
            JobNumber = jobNumber,
            Title = title,
            CustomerId = customerId,
            ProjectId = projectId,
            TechnicianId = technicianId,
            EquipmentDetails = equipmentDetails,
            Type = type,
            Status = technicianId.HasValue ? JobStatus.Assigned : JobStatus.Pending,
            SiteAddress = siteAddress,
            SiteLatitude = siteLatitude,
            SiteLongitude = siteLongitude,
            ScheduledDate = scheduledDate,
            CustomerOtp = customerOtp,
            Notes = notes
        };

        return job;
    }

    public void AssignTechnician(Guid technicianId)
    {
        TechnicianId = technicianId;
        if (Status == JobStatus.Pending)
        {
            Status = JobStatus.Assigned;
        }
    }

    public void StartJob()
    {
        Status = JobStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void CompleteJob(string? enteredOtp = null, string? materialUsage = null)
    {
        if (!string.IsNullOrEmpty(CustomerOtp) && CustomerOtp != enteredOtp)
        {
            throw new InvalidOperationException("Invalid customer OTP confirmation.");
        }

        Status = JobStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(materialUsage))
        {
            MaterialUsage = materialUsage;
        }
    }

    public void CancelJob(string? reason)
    {
        Status = JobStatus.Cancelled;
        Notes = string.IsNullOrEmpty(Notes) ? $"Cancelled: {reason}" : $"{Notes} | Cancelled: {reason}";
    }
}
