using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication.Web;

public sealed class WebAuthenticationRepository(CosmixDbContext db) : IWebAuthenticationRepository
{
    public Task<bool> HasSessionHistoryAsync(Guid accountId, CancellationToken cancellationToken) =>
        db.UserSessions.AnyAsync(x => x.UserAccountId == accountId, cancellationToken);

    public Task<Guid?> FindAccountIdByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.UserAccounts.Where(x => x.NormalizedLoginName == normalizedEmail)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);

    public Task<MobileOtpChallenge?> GetLatestWebChallengeAsync(Guid accountId, CancellationToken cancellationToken) =>
        db.Set<MobileOtpChallenge>().Where(x => x.UserAccountId == accountId && x.Purpose == "WebLogin")
            .OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
}
