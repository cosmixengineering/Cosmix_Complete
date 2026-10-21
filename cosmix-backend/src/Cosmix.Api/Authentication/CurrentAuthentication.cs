using Cosmix.Application.Authentication;

namespace Cosmix.Api.Authentication;

public sealed class CurrentAuthentication(IHttpContextAccessor accessor) : ICurrentAuthentication
{
    public const string ItemKey = "Cosmix.ValidatedAuthentication";
    public AuthenticatedAccount Require() => accessor.HttpContext?.Items[ItemKey] as AuthenticatedAccount
        ?? throw new AuthenticationFailure("invalid_authentication", 401);
}
