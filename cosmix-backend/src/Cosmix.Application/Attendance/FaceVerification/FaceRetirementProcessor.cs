namespace Cosmix.Application.Attendance.FaceVerification;

public sealed class FaceDisposalRunner(FaceDisposalOptions options, IFaceDisposalPolicy policy,
    IFacePhysicalDisposal disposal, TimeProvider clock)
{
    private readonly bool enabled = options.Enabled;
    public async Task<FaceRetirementState> RunAsync(Guid intentId, FaceRetirementIntent intent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); FaceRetirementEvidence.RequireIntent(intent);
        if (!enabled) return FaceRetirementState.PendingPolicy;
        var decision = await policy.EvaluateAsync(intentId, intent, clock.GetUtcNow(), ct);
        if (decision.State is FaceRetirementState.PendingPolicy or FaceRetirementState.PendingHold or FaceRetirementState.PendingBoundary)
        {
            if (decision.Authorization is not null) throw FaceRetirementEvidence.Unavailable();
            return decision.State;
        }
        var grant = decision.Authorization;
        if (decision.State != FaceRetirementState.Disposed || grant is null || grant.IntentId != intentId ||
            !FaceRetirementEvidence.Same(grant.Intent, intent) || grant.NotBeforeUtc.Offset != TimeSpan.Zero ||
            !Approval(grant.RetentionApproval) || !Approval(grant.HoldApproval) || !Approval(grant.BackupApproval) || !Approval(grant.KeyApproval))
            throw FaceRetirementEvidence.Unavailable();
        if (clock.GetUtcNow() < grant.NotBeforeUtc) return FaceRetirementState.PendingBoundary;
        if (!disposal.IsConfigured) return FaceRetirementState.PendingDisposalAdapter;
        ct.ThrowIfCancellationRequested();
        await disposal.DisposeAsync(grant, ct);
        return FaceRetirementState.Disposed;
    }
    private static bool Approval(string? value) => value is { Length: >= 1 and <= 1000 } &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
}

public sealed class FaceRetirementProcessor(IFaceRetirementRepository repository, FaceRetirementService intents,
    IFaceEvidenceRetirement storage, FaceDisposalRunner disposal, TimeProvider clock)
{
    public async Task<FaceRetirementBatch> RunAsync(FaceRetirementCursor? cursor, int size, CancellationToken ct,
        FaceRetirementCursor? revocationCursor = null)
    {
        repository.RequireOutsideTransaction(); ct.ThrowIfCancellationRequested();
        if (size is < 1 or > 100) throw FaceRetirementEvidence.Unavailable();
        FaceRetirementRecovery recovery;
        try { recovery = await intents.RecoverRevocationsAsync(size, ct, revocationCursor); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { ct.ThrowIfCancellationRequested(); recovery = new(0, 0, 1, null); }
        var rows = await repository.IntentsAsync(cursor, size, ct);
        var closed = 0; var pending = 0; var failed = 0;
        foreach (var source in rows)
        {
            ct.ThrowIfCancellationRequested();
            var useClosed = false;
            FaceRetirementState state;
            try
            {
                var intent = await intents.ReadIntentAsync(source, ct);
                var previous = await intents.ReadOutcomeAsync(source, ct);
                if (previous?.State == FaceRetirementState.Disposed) continue;
                useClosed = previous?.UseClosed == true;
                var proof = await repository.ValidateCommittedAsync(intent, ct);
                if (proof == FaceRetirementProof.Pending) state = FaceRetirementState.AwaitingTerminal;
                else if (proof != FaceRetirementProof.Committed) state = FaceRetirementState.ScopeRejected;
                else
                {
                    if (!storage.IsConfigured) throw FaceRetirementEvidence.Unavailable();
                    await storage.RetireAsync(intent.Reference, intent.Purpose, intent.Reason, intent.OperationKey, ct);
                    useClosed = true;
                    // Repeat source validation after storage IO, before the separately gated adapter.
                    if (await repository.ValidateCommittedAsync(intent, ct) != FaceRetirementProof.Committed)
                        state = FaceRetirementState.ScopeRejected;
                    else state = await disposal.RunAsync(source.Id, intent, ct);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { ct.ThrowIfCancellationRequested(); state = FaceRetirementState.RetryRequired; }
            await intents.RecordOutcomeAsync(source, new(1, source.Id, state, useClosed,
                FaceRetirementService.Code(state), clock.GetUtcNow()), ct);
            if (useClosed) closed++;
            if (state is FaceRetirementState.RetryRequired or FaceRetirementState.ScopeRejected) failed++;
            else if (state != FaceRetirementState.Disposed) pending++;
        }
        var last = rows.LastOrDefault();
        return new(rows.Count, closed, pending, failed,
            rows.Count == size && last is not null ? new(last.OccurredAt, last.Id) : null,
            recovery.Pending, recovery.Failed, recovery.NextCursor);
    }
}
