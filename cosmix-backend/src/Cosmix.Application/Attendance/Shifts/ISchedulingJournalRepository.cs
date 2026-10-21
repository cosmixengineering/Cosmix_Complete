using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Idempotency;

namespace Cosmix.Application.Attendance.Shifts;

/// <summary>Scheduling audit and retry evidence use the same scoped database and outer authorization transaction.</summary>
public interface ISchedulingJournalRepository
{
    Task<bool> IsCurrentSessionAsync(AuthenticatedAccount actor, CancellationToken ct);
    Task<IdempotencyReceipt?> FindReceiptAsync(string actor, string operation, string key, CancellationToken ct);
    void AddAudit(AuditEvent audit);
    void AddReceipt(IdempotencyReceipt receipt);
    Task SaveChangesAsync(CancellationToken ct);
}
