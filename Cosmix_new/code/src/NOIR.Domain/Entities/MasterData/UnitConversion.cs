namespace NOIR.Domain.Entities.MasterData;

/// <summary>
/// Unit Conversion Master entity (SRS Sec 20).
/// Defines engineering conversion ratios (e.g. Ton <-> BTU/hr, kW <-> Ton, mm <-> inch, °C <-> °F).
/// </summary>
public class UnitConversion : TenantAggregateRoot<Guid>
{
    private UnitConversion() : base() { }

    private UnitConversion(Guid id, string? tenantId) : base(id, tenantId) { }

    public string FromUnit { get; private set; } = string.Empty;
    public string ToUnit { get; private set; } = string.Empty;
    public decimal ConversionFactor { get; private set; }
    public string Category { get; private set; } = "General";
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    public static UnitConversion Create(
        string fromUnit,
        string toUnit,
        decimal conversionFactor,
        string category,
        string? notes,
        string? tenantId = null)
    {
        return new UnitConversion(Guid.NewGuid(), tenantId)
        {
            FromUnit = fromUnit,
            ToUnit = toUnit,
            ConversionFactor = conversionFactor,
            Category = category,
            Notes = notes,
            IsActive = true
        };
    }

    public decimal Convert(decimal value)
    {
        return value * ConversionFactor;
    }
}
