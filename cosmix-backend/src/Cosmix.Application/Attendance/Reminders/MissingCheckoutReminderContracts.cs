using Cosmix.Domain.Attendance.Shifts;
using Cosmix.Domain.Attendance.WorkSessions;

namespace Cosmix.Application.Attendance.Reminders;

/// <summary>Operational polling only; the business delay is explicit on the pinned ShiftVersion.</summary>
public sealed class MissingCheckoutReminderOptions
{
    public bool Enabled { get; set; }
    public int PollSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 25;
    public bool IsValid() => PollSeconds is >= 5 and <= 3600 && BatchSize is >= 1 and <= 100;
}
public sealed record MissingCheckoutReminderCandidate(Guid EmployeeId, long EmployeeNumber);
public sealed record MissingCheckoutReminderState(WorkSession Session, ShiftVersion Version, Guid RecipientAccountId);
public sealed record MissingCheckoutReminderBatch(int Staged, long NextEmployeeNumber, int ReviewRequired);
public sealed record MissingCheckoutReminderIntent(Guid EmployeeId, Guid RecipientAccountId, Guid SessionKey,
    Guid SourceSessionId, Guid ShiftVersionId, DateOnly BusinessDate, DateTimeOffset DueAtUtc);
public interface IMissingCheckoutReminderRepository
{
    Task<IReadOnlyList<MissingCheckoutReminderCandidate>> CandidatesAsync(long afterEmployeeNumber, int limit,
        DateTimeOffset now, CancellationToken ct);
    /// <summary>Current eligible account and exact latest open duty with its immutable shift version.
    /// Null means no eligible current open duty; conflicting history must fail explicitly.</summary>
    Task<MissingCheckoutReminderState?> LoadAsync(Guid employeeId, DateTimeOffset now, CancellationToken ct);
    /// <summary>Existence-only, non-locking deduplication hint. Never establishes eligibility or delivery.
    /// A dirty positive delays this sweep; a missed existing identity must still fail the unique outbox constraint.</summary>
    Task<bool> AlreadyQueuedAsync(Guid sessionKey, CancellationToken ct);
    /// <summary>Inside the existing outbox transaction, serialize with capture/correction/access changes.</summary>
    Task LockAsync(CancellationToken ct);
}
