namespace NOIR.Domain.Entities.Crm;

/// <summary>
/// A sales pipeline containing ordered stages for lead progression.
/// </summary>
public class Pipeline : TenantAggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    // Navigation properties
    public virtual ICollection<PipelineStage> Stages { get; private set; } = new List<PipelineStage>();

    // Private constructor for EF Core
    private Pipeline() : base() { }

    private Pipeline(Guid id, string? tenantId) : base(id, tenantId) { }

    /// <summary>
    /// Factory method to create a new pipeline.
    /// </summary>
    public static Pipeline Create(
        string name,
        string? tenantId,
        bool isDefault = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Pipeline(Guid.NewGuid(), tenantId)
        {
            Name = name.Trim(),
            IsDefault = isDefault
        };
    }

    /// <summary>
    /// Updates the pipeline name.
    /// </summary>
    public void Update(string name)
    {
        Name = name.Trim();
    }

    /// <summary>
    /// Sets whether this pipeline is the default.
    /// </summary>
    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}
