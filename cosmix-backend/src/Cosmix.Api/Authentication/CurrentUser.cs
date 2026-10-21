using System.Security.Claims;
using Cosmix.Application.Common.Interfaces;

namespace Cosmix.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public string? UserId => User?.FindFirstValue("sub");
    public Guid? EmployeeId => Guid.TryParse(User?.FindFirstValue("employee_id"), out var id) ? id : null;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}
