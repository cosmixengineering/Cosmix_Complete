using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication;

public sealed class PhoneRecoveryRepository(CosmixDbContext db) : IPhoneRecoveryRepository
{
    public void AddChange(LoginPhoneChange change) => db.Add(change);
    public Task<MobileOtpChallenge?> GetLatestChallengeAsync(Guid accountId, CancellationToken ct) =>
        db.Set<MobileOtpChallenge>().Where(x => x.UserAccountId == accountId && x.Purpose == "PhoneRecovery")
            .OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public Task SavePredecessorAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Phone replacement requires an explicit transaction.");
        return db.SaveChangesAsync(ct);
    }
}
