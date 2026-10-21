using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Departments;

public sealed class Department
{
    private Department() { }

    public Department(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        code = code.Trim().ToUpperInvariant();
        name = name.Trim();
        if (code.Length > 30 || name.Length > 150)
            throw new ArgumentException("Department code or name exceeds its maximum length.");

        Id = Guid.NewGuid();
        Code = code;
        Name = name;
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
