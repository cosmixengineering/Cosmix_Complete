using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

public sealed class OperationalZone
{
    private OperationalZone() { }
    public OperationalZone(string code, string name, Guid siteId)
    {
        Id = Guid.NewGuid(); Code = ModelGuard.Code(code, 50); Name = ModelGuard.Required(name, 150, nameof(name));
        SiteId = ModelGuard.Id(siteId, nameof(siteId)); IsActive = true;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid SiteId { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Deactivate() => IsActive = false;
}
