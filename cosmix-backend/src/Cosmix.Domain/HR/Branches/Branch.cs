using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Branches;

public sealed class Branch
{
    private Branch() { }

    public Branch(string code, string name, string? city = null, Guid? cityId = null)
    {
        Id = Guid.NewGuid();
        Code = ModelGuard.Code(code);
        Name = ModelGuard.Required(name, 150, nameof(name));
        City = ModelGuard.Optional(city, 100, nameof(city));
        CityId = cityId.HasValue ? ModelGuard.Id(cityId.Value, nameof(cityId)) : null;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? City { get; private set; }
    public Guid? CityId { get; private set; }
    public City? CityReference { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, Guid? cityId, string? city)
    {
        var validatedName = ModelGuard.Required(name, 150, nameof(name));
        Guid? validatedCityId = cityId.HasValue ? ModelGuard.Id(cityId.Value, nameof(cityId)) : null;
        var validatedCity = ModelGuard.Optional(city, 100, nameof(city));

        Name = validatedName;
        CityId = validatedCityId;
        City = validatedCity;
    }

    public void SetActive(bool active) => IsActive = active;
}
