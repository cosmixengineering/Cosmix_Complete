using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Files;
using Cosmix.Application.Common.Security;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollImports;
using Cosmix.Domain.Finance;
using Cosmix.Domain.HR.PayrollAccounting;

namespace Cosmix.Application.Finance.Reconciliation;

public sealed class BankReconciliationService(SensitiveComplianceOperation reads, PayrollFinanceOperation writes,
    IBankReconciliationRepository repository, IPayrollFinanceRepository finance, IPrivateFileStore files, TimeProvider clock)
{
    public const string Read = "finance.bank-reconciliation.read", Manage = "finance.bank-reconciliation.manage", Close = "finance.bank-reconciliation.close";
    public Task<IReadOnlyList<BankAccountChoice>> AccountsAsync(AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync<IReadOnlyList<BankAccountChoice>>(Read, "finance.bank.accounts", "BankStatement", "accounts", context,
            async (_, token) => (await finance.AccountsAsync(token)).Where(x => x.IsActive && x.Kind == PayrollAccountKind.Bank)
                .Select(x => new BankAccountChoice(x.Code, x.Name)).ToArray(), ct);
    public Task<IReadOnlyList<BankFileSheet>> InspectAsync(BankFileInspectionRequest input, AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync<IReadOnlyList<BankFileSheet>>(Manage, "finance.bank.inspect", "BankStatement", "upload", context,
            (_, _) => Task.FromResult<IReadOnlyList<BankFileSheet>>(PayrollSheetParser.Parse(input.FileName, Bytes(input.FileBase64))
                .Select(x => new BankFileSheet(x.Name, x.Columns)).ToArray()), ct);

    public Task<BankImportPreview> PreviewAsync(BankImportRequest request, AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync(Manage, "finance.bank.preview", "BankStatement", request.AccountCode, context,
            async (_, token) => { await AccountAsync(request, token); return Parse(request); }, ct);

    public Task<IReadOnlyList<BankStatementSummary>> ListAsync(int page, AuthRequestContext context, CancellationToken ct)
    {
        if (page is < 1 or > 4096) throw AttendanceOperation.Invalid();
        return reads.ExecuteAsync<IReadOnlyList<BankStatementSummary>>(Read, "finance.bank.list", "BankStatement", page.ToString(), context, async (_, token) =>
        {
            var result = new List<BankStatementSummary>();
            foreach (var row in await repository.ListAsync(page, token))
                result.Add(Summary(row, await repository.ClosedAsync(row.AccountCode, row.Currency, row.Month, token) is not null));
            return result;
        }, ct);
    }

    public Task<BankStatementSummary> ImportAsync(BankImportRequest input, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Manage, "finance.bank.import", input.AccountCode, key, input, input.Reason, context, async (actor, token) =>
        {
            await AccountAsync(input, token); var parsed = Parse(input);
            await RequireOpenAsync(input.AccountCode, input.Currency, input.Month, token);
            var previous = await repository.LatestAsync(input.AccountCode, input.Currency, input.Month, token);
            if (previous?.Id != input.ExpectedPreviousStatementId) throw AttendanceOperation.Conflict("bank_statement_changed");
            if (await repository.HashExistsAsync(input.AccountCode, input.Currency, input.Month, parsed.FileSha256, token))
            {
                var old = previous?.FileSha256 == parsed.FileSha256 ? await SourceAsync(previous, token) : null;
                if (old is null || old.Mapping == input.Mapping && old.OpeningBalance == input.OpeningBalance && old.ClosingBalance == input.ClosingBalance)
                    throw AttendanceOperation.Conflict("bank_statement_duplicate_import");
                // A corrected mapping or opening/closing balance creates a new retained revision of the same raw file.
            }
            var id = Guid.NewGuid(); var at = clock.GetUtcNow(); var revision = (previous?.Revision ?? 0) + 1;
            var raw = AttendanceOperation.Reference(await files.WriteAsync(Bytes(input.FileBase64), RawPurpose(id), token));
            var proof = new BankSourceProof(id, input.AccountCode, input.Currency, input.Month, revision, parsed.FileSha256,
                raw, input.Mapping, input.OpeningBalance, input.ClosingBalance, actor.AccountId.ToString(), actor.EmployeeId, at, input.Reason.Trim(), parsed.Rows);
            var reference = await WriteAsync(proof, SourcePurpose(id), token);
            var row = new BankStatementSnapshot(id, input.AccountCode, input.Currency, input.Month, revision, parsed.FileSha256,
                reference, actor.AccountId.ToString(), actor.EmployeeId, at);
            repository.Add(row); await repository.SaveChangesAsync(token); return Summary(row, false);
        }, ct);

    public Task<BankReconciliationWorkspace> WorkspaceAsync(Guid id, AuthRequestContext context, CancellationToken ct) =>
        reads.ExecuteAsync(Read, "finance.bank.read", "BankStatement", id.ToString(), context, async (_, token) =>
        {
            var row = await StatementAsync(id, token); var source = await SourceAsync(row, token);
            var ledger = await LedgerAsync(row, token); var fingerprint = Digest(ledger);
            var proposal = await repository.LatestProposalAsync(id, token);
            var proof = proposal is null ? null : await ProposalAsync(proposal, row, token);
            var matching = BankReconciliationProjection.Check(source.Rows, ledger, proof?.Resolutions ?? [], row.Currency);
            var closed = await repository.ClosedAsync(row.AccountCode, row.Currency, row.Month, token);
            if (closed is not null)
            {
                var closeProof = await ReadAsync<BankCloseProof>(closed.ProofReference, ClosePurpose(closed.Id), token);
                if (closed.StatementId != row.Id || proposal?.Id != closed.ProposalId || closeProof.Id != closed.Id ||
                    closeProof.ProposalId != proposal.Id || closeProof.ProposalReference != proposal.ProofReference ||
                    closeProof.StatementId != row.Id || closeProof.LedgerFingerprint != fingerprint || closeProof.Reviewer != closed.ClosedBy ||
                    closeProof.ReviewerEmployeeId != closed.ClosedByEmployeeId || closeProof.At != closed.ClosedAt || !matching.CanClose || Digest(closeProof.Matching) != Digest(matching))
                    throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid");
            }
            return new BankReconciliationWorkspace(Summary(row, closed is not null), source.OpeningBalance, source.ClosingBalance,
                source.Rows, ledger, fingerprint, proposal is null ? null : new(proposal.Id, proposal.CreatedBy, proposal.CreatedByEmployeeId,
                    proposal.CreatedAt, proof!.LedgerFingerprint, proof.Resolutions, proof.Reason), matching, closed?.ClosedBy, closed?.ClosedAt);
        }, ct);

    public Task<BankProposalResponse> ProposeAsync(Guid id, BankProposeRequest input, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Manage, "finance.bank.propose", id.ToString(), key, input, input.Reason, context, async (actor, token) =>
        {
            var row = await StatementAsync(id, token); await CurrentOpenAsync(row, token);
            var source = await SourceAsync(row, token); var ledger = await LedgerAsync(row, token); var digest = Digest(ledger);
            if (digest != input.ExpectedLedgerFingerprint) throw AttendanceOperation.Conflict("bank_ledger_changed");
            var previous = await repository.LatestProposalAsync(id, token);
            if (previous?.Id != input.ExpectedPreviousProposalId) throw AttendanceOperation.Conflict("bank_proposal_changed");
            if (input.Resolutions is null) throw AttendanceOperation.Invalid();
            BankReconciliationProjection.Check(source.Rows, ledger, input.Resolutions, row.Currency);
            var proposalId = Guid.NewGuid(); var at = clock.GetUtcNow();
            var proof = new BankProposalProof(proposalId, row.Id, row.ProofReference, digest, actor.AccountId.ToString(), actor.EmployeeId, at, input.Reason.Trim(), input.Resolutions);
            var reference = await WriteAsync(proof, ProposalPurpose(proposalId), token);
            var proposal = new BankReconciliationProposal(proposalId, id, (previous?.Revision ?? 0) + 1, reference, actor.AccountId.ToString(), actor.EmployeeId, at);
            repository.Add(proposal); await repository.SaveChangesAsync(token);
            return new BankProposalResponse(proposalId, proposal.CreatedBy, proposal.CreatedByEmployeeId, at, digest, input.Resolutions, proof.Reason);
        }, ct);

    public Task<BankStatementSummary> CloseAsync(Guid id, BankCloseRequest input, string key, AuthRequestContext context, CancellationToken ct) =>
        writes.WriteAsync(Close, "finance.bank.close", id.ToString(), key, input, input.Reason, context, async (actor, token) =>
        {
            var row = await StatementAsync(id, token); await CurrentOpenAsync(row, token);
            var proposal = await repository.LatestProposalAsync(id, token);
            if (DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime) < row.Month.AddMonths(1))
                throw AttendanceOperation.Conflict("bank_month_not_finished");
            if (proposal is null || proposal.Id != input.ExpectedProposalId) throw AttendanceOperation.Conflict("bank_proposal_changed");
            if (actor.AccountId.ToString() == row.CreatedBy || actor.EmployeeId == row.CreatedByEmployeeId ||
                actor.AccountId.ToString() == proposal.CreatedBy || actor.EmployeeId == proposal.CreatedByEmployeeId) throw AttendanceOperation.Denied();
            var source = await SourceAsync(row, token); var proof = await ProposalAsync(proposal, row, token);
            var ledger = await LedgerAsync(row, token); var digest = Digest(ledger);
            if (digest != input.ExpectedLedgerFingerprint || digest != proof.LedgerFingerprint) throw AttendanceOperation.Conflict("bank_ledger_changed");
            var check = BankReconciliationProjection.Check(source.Rows, ledger, proof.Resolutions, row.Currency);
            if (!check.CanClose) throw AttendanceOperation.Conflict("bank_reconciliation_unmatched");
            var closeId = Guid.NewGuid(); var at = clock.GetUtcNow();
            var closeProof = new BankCloseProof(closeId, row.Id, proposal.Id, proposal.ProofReference, digest, actor.AccountId.ToString(), actor.EmployeeId, at, input.Reason.Trim(), check);
            var reference = await WriteAsync(closeProof, ClosePurpose(closeId), token);
            repository.Add(new BankReconciliationClose(closeId, row, proposal, reference, actor.AccountId.ToString(), actor.EmployeeId, at));
            await repository.SaveChangesAsync(token); return Summary(row, true);
        }, ct);

    private async Task AccountAsync(BankImportRequest input, CancellationToken ct)
    {
        if (input.AccountCode?.Length is not (> 0 and <= 50) || input.Currency?.Length != 3 || input.Currency.Any(c => c is < 'A' or > 'Z'))
            throw AttendanceOperation.Invalid();
        var account = await finance.AccountAsync(input.AccountCode, ct);
        if (account is null || account.Code != input.AccountCode || !account.IsActive || account.Kind != PayrollAccountKind.Bank)
            throw AttendanceOperation.Conflict("bank_reconciliation_account_invalid");
    }
    private static BankImportPreview Parse(BankImportRequest input)
    {
        var bytes = Bytes(input.FileBase64);
        return new(Convert.ToHexString(SHA256.HashData(bytes)), BankStatementParser.Parse(input.FileName, bytes, input.Mapping, input.Month, input.OpeningBalance, input.ClosingBalance));
    }
    private static byte[] Bytes(string value)
    {
        if (value is null || value.Length > (PayrollSheetParser.MaximumBytes + 2) / 3 * 4) throw AttendanceOperation.Invalid("bank_statement_file_size");
        try { var bytes = Convert.FromBase64String(value); if (bytes.Length is 0 or > PayrollSheetParser.MaximumBytes) throw AttendanceOperation.Invalid("bank_statement_file_size"); return bytes; }
        catch (FormatException) { throw AttendanceOperation.Invalid("bank_statement_file_invalid"); }
    }
    private async Task<BankStatementSnapshot> StatementAsync(Guid id, CancellationToken ct) =>
        await repository.StatementAsync(id, ct) ?? throw AttendanceOperation.Missing("bank_statement_not_found");
    private async Task RequireOpenAsync(string account, string currency, DateOnly month, CancellationToken ct)
    { if (await repository.ClosedAsync(account, currency, month, ct) is not null) throw AttendanceOperation.Conflict("bank_month_closed"); }
    private async Task CurrentOpenAsync(BankStatementSnapshot row, CancellationToken ct)
    {
        await RequireOpenAsync(row.AccountCode, row.Currency, row.Month, ct);
        if ((await repository.LatestAsync(row.AccountCode, row.Currency, row.Month, ct))?.Id != row.Id) throw AttendanceOperation.Conflict("bank_statement_superseded");
    }
    private async Task<IReadOnlyList<BankLedgerMovement>> LedgerAsync(BankStatementSnapshot row, CancellationToken ct)
    {
        var ledger = await repository.LedgerAsync(row.AccountCode, row.Currency, row.Month, ct);
        if (ledger.Count > 10000) throw AttendanceOperation.Conflict("bank_ledger_limit");
        return ledger.OrderBy(x => x.JournalId).ToArray();
    }
    private async Task<BankSourceProof> SourceAsync(BankStatementSnapshot row, CancellationToken ct)
    {
        var proof = await ReadAsync<BankSourceProof>(row.ProofReference, SourcePurpose(row.Id), ct);
        if (proof.Id != row.Id || proof.AccountCode != row.AccountCode || proof.Currency != row.Currency || proof.Month != row.Month ||
            proof.Revision != row.Revision || proof.FileSha256 != row.FileSha256 || proof.Maker != row.CreatedBy || proof.MakerEmployeeId != row.CreatedByEmployeeId || proof.At != row.CreatedAt ||
            proof.OpeningBalance + proof.Rows.Sum(x => x.Amount) != proof.ClosingBalance)
            throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid");
        var raw = await ReadBytesAsync(proof.RawReference, RawPurpose(row.Id), ct);
        if (Convert.ToHexString(SHA256.HashData(raw)) != proof.FileSha256) throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid");
        return proof;
    }
    private async Task<BankProposalProof> ProposalAsync(BankReconciliationProposal row, BankStatementSnapshot source, CancellationToken ct)
    {
        var proof = await ReadAsync<BankProposalProof>(row.ProofReference, ProposalPurpose(row.Id), ct);
        if (proof.Id != row.Id || proof.StatementId != source.Id || proof.SourceReference != source.ProofReference || proof.Maker != row.CreatedBy ||
            proof.MakerEmployeeId != row.CreatedByEmployeeId || proof.At != row.CreatedAt) throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid");
        return proof;
    }
    private Task<byte[]> ReadBytesAsync(string reference, string purpose, CancellationToken ct)
    {
        try { return files.ReadAsync(JsonSerializer.Deserialize<PrivateFileReference>(reference) ?? throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid"), purpose, ct); }
        catch (JsonException) { throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid"); }
    }
    private async Task<T> ReadAsync<T>(string reference, string purpose, CancellationToken ct)
    {
        try { return JsonSerializer.Deserialize<T>(await ReadBytesAsync(reference, purpose, ct)) ?? throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid"); }
        catch (JsonException) { throw AttendanceOperation.Conflict("bank_reconciliation_evidence_invalid"); }
    }
    private async Task<string> WriteAsync<T>(T value, string purpose, CancellationToken ct) => AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(value), purpose, ct));
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static BankStatementSummary Summary(BankStatementSnapshot x, bool closed) => new(x.Id, x.AccountCode, x.Currency, x.Month, x.Revision, x.FileSha256, x.CreatedBy, x.CreatedAt, closed);
    private static string SourcePurpose(Guid id) => $"finance:bank:source:{id:N}";
    private static string RawPurpose(Guid id) => $"finance:bank:raw:{id:N}";
    private static string ProposalPurpose(Guid id) => $"finance:bank:proposal:{id:N}";
    private static string ClosePurpose(Guid id) => $"finance:bank:close:{id:N}";
}
