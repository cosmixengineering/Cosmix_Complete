using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.EmploymentTypes;

public sealed class EmploymentType
{
    private EmploymentType() { }

    public EmploymentType(string code, string name)
    {
        Id = Guid.NewGuid();
        Code = ModelGuard.Code(code);
        Name = ModelGuard.Required(name, 150, nameof(name));
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name) => Name = ModelGuard.Required(name, 150, nameof(name));

    public void SetActive(bool active) => IsActive = active;
}
