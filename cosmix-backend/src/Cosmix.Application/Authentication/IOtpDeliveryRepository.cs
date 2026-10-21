using Cosmix.Domain.Authentication.Mobile;

namespace Cosmix.Application.Authentication;

public interface IOtpDeliveryRepository
{
    Task<IReadOnlyList<OtpDelivery>> GetNonterminalAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<OtpDelivery?> GetAsync(Guid id, CancellationToken cancellationToken);
}
