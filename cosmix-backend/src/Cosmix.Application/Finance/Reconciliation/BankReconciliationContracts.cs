using Cosmix.Domain.Finance;

namespace Cosmix.Application.Finance.Reconciliation;

public sealed record BankStatementMapping(string Sheet, string DateColumn, string ReferenceColumn, string DebitColumn, string CreditColumn, string DateFormat);
public sealed record BankStatementRow(int Row, DateOnly Date, string Reference, decimal Amount);
public sealed record BankLedgerMovement(Guid JournalId, DateOnly Date, string Reference, decimal Amount, string Currency);
public sealed record BankRowResolution(int Row, IReadOnlyList<Guid> JournalIds, string? OutsidePayrollReason, string? ReferenceDifferenceReason);
public sealed record BankMatchingReport(bool CanClose, decimal MatchedBankNet, decimal MatchedLedgerNet, decimal OutsidePayrollNet,
    IReadOnlyList<int> UnmatchedRows, IReadOnlyList<Guid> UnmatchedLedgerIds);
public sealed record BankImportRequest(string AccountCode, string Currency, DateOnly Month, decimal OpeningBalance, decimal ClosingBalance,
    string FileName, string FileBase64, BankStatementMapping Mapping, Guid? ExpectedPreviousStatementId, string Reason);
public sealed record BankImportPreview(string FileSha256, IReadOnlyList<BankStatementRow> Rows);
public sealed record BankFileInspectionRequest(string FileName, string FileBase64);
public sealed record BankFileSheet(string Name, IReadOnlyList<string> Columns);
public sealed record BankAccountChoice(string Code, string Name);
public sealed record BankProposeRequest(Guid? ExpectedPreviousProposalId, string ExpectedLedgerFingerprint, IReadOnlyList<BankRowResolution> Resolutions, string Reason);
public sealed record BankCloseRequest(Guid ExpectedProposalId, string ExpectedLedgerFingerprint, string Reason);
public sealed record BankStatementSummary(Guid Id, string AccountCode, string Currency, DateOnly Month, int Revision, string FileSha256, string CreatedBy, DateTimeOffset CreatedAt, bool Closed);
public sealed record BankProposalResponse(Guid Id, string CreatedBy, Guid CreatedByEmployeeId, DateTimeOffset CreatedAt, string LedgerFingerprint, IReadOnlyList<BankRowResolution> Resolutions, string Reason);
public sealed record BankReconciliationWorkspace(BankStatementSummary Statement, decimal OpeningBalance, decimal ClosingBalance,
    IReadOnlyList<BankStatementRow> Rows, IReadOnlyList<BankLedgerMovement> Ledger, string LedgerFingerprint,
    BankProposalResponse? Proposal, BankMatchingReport Matching, string? ClosedBy, DateTimeOffset? ClosedAt);
public sealed record BankSourceProof(Guid Id, string AccountCode, string Currency, DateOnly Month, int Revision, string FileSha256,
    string RawReference, BankStatementMapping Mapping, decimal OpeningBalance, decimal ClosingBalance,
    string Maker, Guid MakerEmployeeId, DateTimeOffset At, string Reason, IReadOnlyList<BankStatementRow> Rows);
public sealed record BankProposalProof(Guid Id, Guid StatementId, string SourceReference, string LedgerFingerprint,
    string Maker, Guid MakerEmployeeId, DateTimeOffset At, string Reason, IReadOnlyList<BankRowResolution> Resolutions);
public sealed record BankCloseProof(Guid Id, Guid StatementId, Guid ProposalId, string ProposalReference, string LedgerFingerprint,
    string Reviewer, Guid ReviewerEmployeeId, DateTimeOffset At, string Reason, BankMatchingReport Matching);

public interface IBankReconciliationRepository
{
    Task<IReadOnlyList<BankStatementSnapshot>> ListAsync(int page, CancellationToken ct);
    Task<BankStatementSnapshot?> StatementAsync(Guid id, CancellationToken ct);
    Task<BankStatementSnapshot?> LatestAsync(string account, string currency, DateOnly month, CancellationToken ct);
    Task<bool> HashExistsAsync(string account, string currency, DateOnly month, string hash, CancellationToken ct);
    Task<BankReconciliationProposal?> LatestProposalAsync(Guid statementId, CancellationToken ct);
    Task<BankReconciliationClose?> ClosedAsync(string account, string currency, DateOnly month, CancellationToken ct);
    Task<IReadOnlyList<BankLedgerMovement>> LedgerAsync(string account, string currency, DateOnly month, CancellationToken ct);
    void Add(BankStatementSnapshot row);
    void Add(BankReconciliationProposal row);
    void Add(BankReconciliationClose row);
    Task SaveChangesAsync(CancellationToken ct);
}
