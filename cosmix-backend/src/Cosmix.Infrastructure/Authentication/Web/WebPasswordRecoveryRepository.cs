using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication.Web;

public sealed class WebPasswordRecoveryRepository(CosmixDbContext db) : IWebPasswordRecoveryRepository
{
    public Task<MobileOtpChallenge?> GetLatestResetChallengeAsync(Guid accountId, CancellationToken cancellationToken) =>
        db.Set<MobileOtpChallenge>().Where(x => x.UserAccountId == accountId && x.Purpose == "WebPasswordReset")
            .OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
}
