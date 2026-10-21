using Cosmix.Application.Authentication;

namespace Cosmix.Application.Attendance.Sites;

/// <summary>Reuse live web/mobile session, employment and auth.self.read checks; never accept a client employee ID.</summary>
public sealed class SiteAttendanceIdentity(AccountSessionService accounts) : ISiteAttendanceIdentity
{
    public async Task<Guid> RequireEmployeeAsync(CancellationToken ct)
    {
        var account = await accounts.MeAsync(ct);
        if (account.EmployeeId == Guid.Empty) throw new AuthenticationFailure("invalid_authentication", 401);
        return account.EmployeeId;
    }
}
