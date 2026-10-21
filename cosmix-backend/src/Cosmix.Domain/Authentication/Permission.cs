using Cosmix.Domain.Common;

namespace Cosmix.Domain.Authentication;

/// <summary>Approved server permission catalog. No role name implicitly grants a permission.</summary>
public sealed class Permission
{
    private Permission() { }
    public Permission(string code, string description, bool isSensitive)
    {
        Id = Guid.NewGuid(); Code = ModelGuard.Required(code, 150, nameof(code)).ToLowerInvariant();
        Description = ModelGuard.Required(description, 500, nameof(description)); IsSensitive = isSensitive;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool IsSensitive { get; private set; }
}
