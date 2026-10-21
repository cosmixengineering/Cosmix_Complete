using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication.Mobile;
using Cosmix.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Cosmix.Infrastructure.Authentication;

public sealed class OtpDeliveryRepository(CosmixDbContext db) : IOtpDeliveryRepository
{
    public async Task<IReadOnlyList<OtpDelivery>> GetNonterminalAsync(DateTimeOffset now, CancellationToken ct) =>
        await db.Set<OtpDelivery>().Where(x => x.Status == OtpDeliveryStatus.Pending ||
            (x.Status == OtpDeliveryStatus.Dispatching && (x.LeaseUntil <= now || x.ExpiresAt <= now)))
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
    public Task<OtpDelivery?> GetAsync(Guid id, CancellationToken ct) => db.Set<OtpDelivery>().SingleOrDefaultAsync(x => x.Id == id, ct);
}
