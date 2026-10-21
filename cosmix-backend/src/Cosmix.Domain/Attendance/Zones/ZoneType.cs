using Cosmix.Domain.Common;

namespace Cosmix.Domain.Attendance.Zones;

public enum ZoneEntryRule { Permitted = 1, PermissionRequired = 2, Prohibited = 3 }

/// <summary>Configurable types (including Green/Amber/Red); no role or color is hardcoded into a client contract.</summary>
public sealed class ZoneType
{
    private ZoneType() { }
    public ZoneType(string code, string name, string displayColor, ZoneEntryRule entryRule)
    {
        if (!Enum.IsDefined(entryRule)) throw new ArgumentOutOfRangeException(nameof(entryRule));
        Id = Guid.NewGuid(); Code = ModelGuard.Code(code, 30); Name = ModelGuard.Required(name, 100, nameof(name));
        DisplayColor = ModelGuard.Required(displayColor, 30, nameof(displayColor)); EntryRule = entryRule; IsActive = true;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string DisplayColor { get; private set; } = string.Empty;
    public ZoneEntryRule EntryRule { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Deactivate() => IsActive = false;
}
