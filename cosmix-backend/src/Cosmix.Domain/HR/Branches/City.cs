using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Branches;

/// <summary>Controlled city master for branches, sites and employee addresses (SRS 6.1).</summary>
public sealed class City
{
    private City() { }

    public City(string code, string name, string? province = null)
    {
        Id = Guid.NewGuid();
        Code = ModelGuard.Code(code);
        Name = ModelGuard.Required(name, 100, nameof(name));
        Province = ModelGuard.Optional(province, 100, nameof(province));
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Province { get; private set; }
    public bool IsActive { get; private set; } = true;
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, string? province)
    {
        var validatedName = ModelGuard.Required(name, 100, nameof(name));
        var validatedProvince = ModelGuard.Optional(province, 100, nameof(province));

        Name = validatedName;
        Province = validatedProvince;
    }

    public void SetActive(bool active) => IsActive = active;

    public void Deactivate() => SetActive(false);
}
