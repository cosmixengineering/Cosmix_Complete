using Cosmix.Domain.Common;

namespace Cosmix.Domain.HR.Policies;

public sealed class Policy
{
    private Policy() { }

    public Policy(string code, string name, PolicyCategory category)
    {
        Id = Guid.NewGuid();
        Code = ModelGuard.Code(code, 40);
        Name = ModelGuard.Required(name, 200, nameof(name));
        Category = Enum.IsDefined(category) ? category : throw new ArgumentOutOfRangeException(nameof(category));
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PolicyCategory Category { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public int Revision { get; private set; }
    public void AdvanceRevision() => Revision = checked(Revision + 1);

    public bool IsActive { get; private set; } = true;
    public ICollection<PolicyVersion> Versions { get; private set; } = new List<PolicyVersion>();

    public void Deactivate() => IsActive = false;
}
