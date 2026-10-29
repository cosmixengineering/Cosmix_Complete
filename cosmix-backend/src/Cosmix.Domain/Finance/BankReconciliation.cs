using Cosmix.Domain.Common;

namespace Cosmix.Domain.Finance;

public sealed class BankStatementSnapshot : IAppendOnlyEntity
{
    private BankStatementSnapshot() { }
    public BankStatementSnapshot(Guid id, string account, string currency, DateOnly month, int revision, string hash,
        string proof, string actor, Guid employee, DateTimeOffset at)
    {
        if (month.Day != 1 || month.Year > 9998 || revision < 1) throw new ArgumentException("Invalid statement month or revision.");
        Id = ModelGuard.Id(id, nameof(id)); AccountCode = ModelGuard.Required(account, 50, nameof(account));
        Currency = ModelGuard.Required(currency, 3, nameof(currency)); Month = month; Revision = revision;
        FileSha256 = ModelGuard.Required(hash, 64, nameof(hash)); ProofReference = ModelGuard.Required(proof, 2000, nameof(proof));
        CreatedBy = ModelGuard.Required(actor, 200, nameof(actor)); CreatedByEmployeeId = ModelGuard.Id(employee, nameof(employee)); CreatedAt = ModelGuard.Utc(at);
    }
    public Guid Id { get; private set; }
    public string AccountCode { get; private set; } = "";
    public string Currency { get; private set; } = "";
    public DateOnly Month { get; private set; }
    public int Revision { get; private set; }
    public string FileSha256 { get; private set; } = "";
    public string ProofReference { get; private set; } = "";
    public string CreatedBy { get; private set; } = "";
    public Guid CreatedByEmployeeId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class BankReconciliationProposal : IAppendOnlyEntity
{
    private BankReconciliationProposal() { }
    public BankReconciliationProposal(Guid id, Guid statement, int revision, string proof, string actor, Guid employee, DateTimeOffset at)
    {
        if (revision < 1) throw new ArgumentException("Invalid proposal revision.");
        Id = ModelGuard.Id(id, nameof(id)); StatementId = ModelGuard.Id(statement, nameof(statement)); Revision = revision;
        ProofReference = ModelGuard.Required(proof, 2000, nameof(proof)); CreatedBy = ModelGuard.Required(actor, 200, nameof(actor));
        CreatedByEmployeeId = ModelGuard.Id(employee, nameof(employee)); CreatedAt = ModelGuard.Utc(at);
    }
    public Guid Id { get; private set; }
    public Guid StatementId { get; private set; }
    public int Revision { get; private set; }
    public string ProofReference { get; private set; } = "";
    public string CreatedBy { get; private set; } = "";
    public Guid CreatedByEmployeeId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class BankReconciliationClose : IAppendOnlyEntity
{
    private BankReconciliationClose() { }
    public BankReconciliationClose(Guid id, BankStatementSnapshot statement, BankReconciliationProposal proposal, string proof,
        string reviewer, Guid employee, DateTimeOffset at)
    {
        if (statement.Id != proposal.StatementId || reviewer == proposal.CreatedBy || employee == proposal.CreatedByEmployeeId ||
            reviewer == statement.CreatedBy || employee == statement.CreatedByEmployeeId || at < proposal.CreatedAt)
            throw new InvalidOperationException("Statement closing requires an independent reviewer of the current proposal.");
        Id = ModelGuard.Id(id, nameof(id)); StatementId = statement.Id; ProposalId = proposal.Id;
        AccountCode = statement.AccountCode; Currency = statement.Currency; Month = statement.Month;
        ProofReference = ModelGuard.Required(proof, 2000, nameof(proof)); ClosedBy = ModelGuard.Required(reviewer, 200, nameof(reviewer));
        ClosedByEmployeeId = ModelGuard.Id(employee, nameof(employee)); ClosedAt = ModelGuard.Utc(at);
    }
    public Guid Id { get; private set; }
    public Guid StatementId { get; private set; }
    public Guid ProposalId { get; private set; }
    public string AccountCode { get; private set; } = "";
    public string Currency { get; private set; } = "";
    public DateOnly Month { get; private set; }
    public string ProofReference { get; private set; } = "";
    public string ClosedBy { get; private set; } = "";
    public Guid ClosedByEmployeeId { get; private set; }
    public DateTimeOffset ClosedAt { get; private set; }
}
