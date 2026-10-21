using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication.Scopes;

public sealed class Team
{
    private Team() { }
    public Team(string code, string name)
    {
        Id = Guid.NewGuid(); Code = ModelGuard.Code(code, 50); Name = ModelGuard.Required(name, 150, nameof(name)); IsActive = true;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public void Deactivate() => IsActive = false;
}
