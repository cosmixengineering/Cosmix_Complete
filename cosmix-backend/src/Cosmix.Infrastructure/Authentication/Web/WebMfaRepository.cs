using Cosmix.Application.Authentication.Web;
using Cosmix.Domain.Authentication;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication.Web;

public sealed class WebMfaRepository(CosmixDbContext db) : IWebMfaRepository
{
    public async Task<IReadOnlyList<MfaCredential>> CredentialsAsync(Guid accountId, CancellationToken ct) =>
        await db.Set<MfaCredential>().Where(x => x.UserAccountId == accountId && x.RevokedAt == null)
            .OrderBy(x => x.EnrolledAt).ThenBy(x => x.Id).Take(101).ToArrayAsync(ct);
    public Task<MfaChallenge?> ChallengeAsync(Guid id, CancellationToken ct) => db.Set<MfaChallenge>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<MfaChallenge>> OpenChallengesAsync(Guid accountId, CancellationToken ct) =>
        await db.Set<MfaChallenge>().Where(x => x.UserAccountId == accountId && x.Purpose == "WebLoginTotp" && x.ConsumedAt == null && x.RevokedAt == null).ToArrayAsync(ct);
    public Task<MfaChallenge?> LatestChallengeAsync(Guid accountId, CancellationToken ct) =>
        db.Set<MfaChallenge>().Where(x => x.UserAccountId == accountId && x.Purpose == "WebLoginTotp").OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
    public void Add(MfaCredential credential) => db.Add(credential);
    public void Add(MfaChallenge challenge) => db.Add(challenge);
    public void Add(MfaResetApproval approval) => db.Add(approval);
}
