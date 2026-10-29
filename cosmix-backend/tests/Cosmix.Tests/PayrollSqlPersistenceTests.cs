using Cosmix.Domain.HR.PayrollImports;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;
using Cosmix.Domain.Attendance.MonthLocks;
using Cosmix.Domain.Attendance.DailySummaries;
using Cosmix.Domain.Common.Audit;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.Attendance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollSqlFactAttribute : FactAttribute
{
    public PayrollSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_PAYROLL_VERIFICATION_CONNECTION")))
            Skip = "Requires an explicitly selected disposable payroll verification database.";
    }
}

[Trait("Category", "SqlIntegration")]
public sealed class PayrollSqlPersistenceTests
{
    [PayrollSqlFact]
    public async Task Authenticator_session_authority_translates_to_SQL_and_reset_evidence_is_append_only()
    {
        await using var db = Context(); await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var accounts = await db.UserAccounts.Where(a => a.Status == Cosmix.Domain.Authentication.AccountStatus.Active && a.WebAccessEnabled)
            .OrderBy(a => a.Id).Take(2).ToArrayAsync();
        Assert.Equal(2, accounts.Length);
        var auth = new Cosmix.Infrastructure.Authentication.AuthenticationRepository(db);
        var state = (await auth.GetAccountAsync(accounts[0].Id, now, default))!;
        var credential = new Cosmix.Domain.Authentication.MfaCredential(state.Account.Id, Cosmix.Domain.Authentication.MfaMethod.Totp, "synthetic-sql-secret-reference", null, now);
        credential.Confirm(now); credential.RecordAcceptedProof(now, now.ToUnixTimeSeconds() / 30 - 1);
        state.Account.EnableAuthenticator(credential, state.Phone!, now); db.Add(credential);
        var challenge = new Cosmix.Domain.Authentication.MfaChallenge(state.Account, null, "SQL-MFA-" + Guid.NewGuid().ToString("N"), now, now.AddMinutes(5));
        challenge.BindWebLogin(state.Phone!.Id, new string('A', 64));
        challenge.Verify(credential, now, now.ToUnixTimeSeconds() / 30);
        var session = new Cosmix.Domain.Authentication.UserSession(state.Account, Cosmix.Domain.Authentication.SessionClient.Web, null, challenge, now, now.AddMinutes(20), "sql-mfa-fixture");
        db.AddRange(challenge, session); await db.SaveChangesAsync();
        var actor = new Cosmix.Application.Authentication.AuthenticatedAccount(state.Account.Id, state.Account.EmployeeId, session.Id,
            state.Account.SecurityVersion, Cosmix.Domain.Authentication.SessionClient.Web, true);
        var access = new Cosmix.Infrastructure.Authentication.Access.AccessManagementRepository(db, new() { MaximumOtpAttempts = 5 });
        var method = access.GetType().GetMethod("CurrentAdministratorQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        IQueryable<Guid> Query() => (IQueryable<Guid>)method.Invoke(access, [actor, now, DateOnly.FromDateTime(now.UtcDateTime)])!;
        Assert.Equal(session.Id, await Query().SingleAsync());
        var approval = new Cosmix.Domain.Authentication.MfaResetApproval(Guid.NewGuid(), state.Account, accounts[1].Id, accounts[1].EmployeeId, "synthetic-sql-review", now);
        db.Add(approval); state.Account.CompleteAuthenticatorReset(approval); credential.Revoke(now); session.Revoke("synthetic-mfa-reset", now);
        await db.SaveChangesAsync(); Assert.False(await Query().AnyAsync());
        db.Entry(approval).Property(x => x.ProofReference).CurrentValue = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [PayrollSqlFact]
    public async Task Bank_reconciliation_queries_persist_and_closed_month_rejects_backdated_cash_journals()
    {
        await using var db = Context(); await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow; var bank = "SQL-BANK-" + Guid.NewGuid().ToString("N");
        var month = new DateOnly(2098, 4, 1);
        var batch = new PayrollHandoffBatch("SQL-BANK-" + Guid.NewGuid().ToString("N"), month, month.AddMonths(1), "PKR", "bank-fixture", now);
        var obligation = new PayrollObligation(batch.Id, 100m, 100m, "PKR", "bank-fixture", now);
        var entry = new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryPayment, "SQL-PAYABLE", bank, 100m, "PKR", "Bank SQL fixture", "bank-fixture", now);
        entry.BindPosting(month, Guid.NewGuid(), null, "SQL-BANK-ENTRY-" + Guid.NewGuid().ToString("N"));
        db.AddRange(batch, obligation, entry); await db.SaveChangesAsync();
        var repo = new Cosmix.Infrastructure.Finance.BankReconciliationRepository(db);
        Assert.Equal(-100m, Assert.Single(await repo.LedgerAsync(bank, "PKR", month, default)).Amount);
        var statement = new Cosmix.Domain.Finance.BankStatementSnapshot(Guid.NewGuid(), bank, "PKR", month, 1, new('A', 64), "sql-source", "maker", Guid.NewGuid(), now);
        var proposal = new Cosmix.Domain.Finance.BankReconciliationProposal(Guid.NewGuid(), statement.Id, 1, "sql-matches", "maker", statement.CreatedByEmployeeId, now);
        var closed = new Cosmix.Domain.Finance.BankReconciliationClose(Guid.NewGuid(), statement, proposal, "sql-close", "reviewer", Guid.NewGuid(), now);
        repo.Add(statement); repo.Add(proposal); repo.Add(closed); await repo.SaveChangesAsync(default);
        Assert.Equal(statement.Id, (await repo.LatestAsync(bank, "PKR", month, default))!.Id);
        Assert.Equal(proposal.Id, (await repo.LatestProposalAsync(statement.Id, default))!.Id);
        Assert.Equal(closed.Id, (await repo.ClosedAsync(bank, "PKR", month, default))!.Id);
        Assert.True(await repo.HashExistsAsync(bank, "PKR", month, new('A', 64), default));
        Assert.Contains(await repo.ListAsync(1, default), x => x.Id == statement.Id);
        db.Entry(closed).Property(x => x.ProofReference).CurrentValue = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.Entry(closed).State = EntityState.Detached;
        var late = new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryPayment, "SQL-PAYABLE", bank, 1m, "PKR", "Late SQL fixture", "bank-fixture", now);
        late.BindPosting(month, Guid.NewGuid(), null, "SQL-LATE-" + Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<Cosmix.Application.HR.PayrollAccounting.PayrollFinanceConflictException>(() => new PayrollAccountingRepository(db).AddJournalEntryAsync(late, default));
        db.Add(late);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(51016, Assert.IsType<SqlException>(error.InnerException).Number);
        await transaction.RollbackAsync();
    }

    [PayrollSqlFact]
    public async Task Returned_salary_preserves_approval_releases_period_and_checks_concurrent_version()
    {
        await using var db = Context();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var employee = await db.Employees.AsNoTracking().Select(x => x.Id).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var period = new AttendancePeriodLock(new(2097, 3, 1), new(2097, 4, 1));
        var locked = period.RecordDirectLock(Guid.NewGuid(), "return-fixture-maker", "SQL return fixture", now, "synthetic-month");
        var summary = new DailyAttendanceSummary(employee, period.PeriodStart, 1, AttendanceDayStatus.Unverified,
            null, null, "synthetic-return", new('A', 64), 0, 0, null, null, null, 0, now);
        db.AddRange(period, locked, summary, new AttendancePeriodSummary(period, locked, summary));
        var batch = new PayrollHandoffBatch("SQL-RETURN-" + Guid.NewGuid().ToString("N"), period.PeriodStart,
            period.PeriodEndExclusive, "PKR", "return-fixture-maker", now);
        batch.Lines.Add(new(batch.Id, employee, PayrollComponentKind.BaseCompensation, "attendance.payroll-salary.base",
            Guid.NewGuid(), 1, locked.Id, 1000m, "PKR", null, null, "synthetic-line", Guid.NewGuid().ToString("N")));
        batch.SetSalaryEvidence(new('A', 64), new('B', 64), "synthetic-salary");
        db.Add(batch); await db.SaveChangesAsync();
        var staleVersion = batch.RowVersion.ToArray();
        var hr = new ApprovalRequest("payroll-handoff.hr-review", batch.Id, batch.PreparedByUserId, "synthetic-hr", 1, now);
        db.AddRange(hr, hr.Decide(ApprovalState.Approved, "independent-hr", "HR", "Approved fixture", now));
        batch.ReviewSalary(hr.Id, "independent-hr", now, true);
        await db.SaveChangesAsync();
        var repository = new PayrollHandoffRepository(db);
        Assert.True(await repository.PeriodPreparedAsync(batch.PeriodStart, batch.PeriodEnd, default));
        Assert.True(await repository.EmployeesPreparedAsync(batch.PeriodStart, batch.PeriodEnd, [employee], default));
        Assert.False(await repository.EmployeesPreparedAsync(batch.PeriodStart, batch.PeriodEnd, [Guid.NewGuid()], default));
        var returned = new ApprovalRequest("payroll-handoff.return-for-correction", batch.Id, batch.PreparedByUserId,
            "synthetic-return", 1, now);
        db.AddRange(returned, returned.Decide(ApprovalState.Returned, "independent-hr", "HR", "Correct fixture", now));
        batch.ReturnForCorrection(returned.Id, "independent-hr", now);
        repository.ExpectVersion(batch, staleVersion);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        await db.Entry(batch).ReloadAsync();
        Assert.Equal("hr_approved", batch.ReviewState);
        batch.ReturnForCorrection(returned.Id, "independent-hr", now);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var persisted = await db.PayrollHandoffBatches.Include(x => x.Lines).SingleAsync(x => x.Id == batch.Id);
        Assert.Equal("returned", persisted.ReviewState);
        Assert.Equal(hr.Id, persisted.HrApprovalRequestId);
        Assert.Equal(returned.Id, persisted.ReturnApprovalRequestId);
        Assert.Equal(1000m, Assert.Single(persisted.Lines).Amount);
        Assert.False(await repository.PeriodPreparedAsync(batch.PeriodStart, batch.PeriodEnd, default));
        Assert.False(await repository.EmployeesPreparedAsync(batch.PeriodStart, batch.PeriodEnd, [employee], default));
        Assert.False(await new PayrollSalaryRepository(db).HasPreparedPeriodAsync(batch.PeriodStart, batch.PeriodEnd, default));
        Assert.Empty((await new HrDashboardPayrollRepository(db).ReadAsync([employee], batch.PeriodStart,
            batch.PeriodStart, batch.PeriodEnd, default)).Batches);
        Assert.DoesNotContain((await new EmployeeSelfServiceRepository(db, TimeProvider.System)
            .GetPayrollHistoryAsync(employee, 1, 100, default)).Items, row => row.BatchId == batch.Id);
        await transaction.RollbackAsync();
    }

    private static CosmixDbContext Context()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_PAYROLL_VERIFICATION_CONNECTION")
            ?? throw new InvalidOperationException("Disposable SQL target required.");
        var parsed = new SqlConnectionStringBuilder(connection);
        if (!parsed.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            !parsed.InitialCatalog.StartsWith("Cosmix_Payroll_", StringComparison.Ordinal) || !parsed.IntegratedSecurity)
            throw new InvalidOperationException("Payroll verification cannot run against a business database.");
        return new(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options);
    }

    [PayrollSqlFact]
    public async Task Finance_repository_saves_leave_business_and_audit_rollback_to_the_caller()
    {
        Guid contributionId, entryId, auditId;
        await using (var db = Context())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var employee = await db.Employees.AsNoTracking().Select(x => x.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            var statutory = new StatutoryContributionRepository(db);
            var registration = await statutory.GetRegistrationAsync(employee, StatutoryScheme.EOBI, default);
            if (registration is null)
            {
                registration = new(employee, StatutoryScheme.EOBI, "SYNTHETIC-" + Guid.NewGuid().ToString("N"),
                    new(2098, 2, 1), "rollback-fixture", now);
                await statutory.AddRegistrationAsync(registration, default);
            }
            var contribution = new StatutoryContribution(StatutoryScheme.EOBI, new(2098, 2, 1), 100m, 500m,
                "PKR", "rollback-fixture", now, hasEmployeeEntries: true);
            var entry = new StatutoryContributionEntry(contribution.Id, employee, registration.Id, registration.RegistrationNumber,
                100m, 500m, "synthetic-rollback-source", new('A', 64), "synthetic-rollback-private-proof");
            contribution.Entries.Add(entry);
            await statutory.AddContributionAsync(contribution, default);
            await statutory.SaveChangesAsync(default);
            Assert.Same(transaction, db.Database.CurrentTransaction);
            Assert.Equal(8, contribution.RowVersion.Length);

            contribution.Approve("independent-rollback-reviewer", now.AddSeconds(1));
            await new PayrollAccountingRepository(db).SaveChangesAsync(default);
            Assert.Same(transaction, db.Database.CurrentTransaction);
            var audit = new AuditEvent("synthetic.finance.rollback", "rollback-fixture", "SyntheticFixture", "Contribution",
                contribution.Id.ToString(), now.AddSeconds(1), Guid.NewGuid().ToString("N"), employee);
            var journal = new SchedulingJournalRepository(db, null!, null!);
            journal.AddAudit(audit);
            await journal.SaveChangesAsync(default);
            Assert.Same(transaction, db.Database.CurrentTransaction);
            Assert.True(await db.StatutoryContributions.AsNoTracking().AnyAsync(x => x.Id == contribution.Id));
            Assert.True(await db.AuditEvents.AsNoTracking().AnyAsync(x => x.Id == audit.Id));
            var batch = new PayrollHandoffBatch("SQL-COVERAGE-" + Guid.NewGuid().ToString("N"),
                new(2098, 2, 1), new(2098, 3, 1), "PKR", "rollback-fixture", now);
            var obligation = new PayrollObligation(batch.Id, 1000m, 900m, "PKR", "rollback-fixture", now);
            db.AddRange(batch, obligation); await db.SaveChangesAsync();
            var salaryJournal = new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryAccrual,
                "SQL-SALARY", "SQL-PAYABLE", 1000m, "PKR", "SQL shared ledger fixture", "independent-finance", now);
            salaryJournal.BindPosting(new(2098, 2, 1), Guid.NewGuid(), null, "SQL-LEDGER-" + Guid.NewGuid().ToString("N"));
            db.Add(salaryJournal); await db.SaveChangesAsync();
            var legs = await db.Set<Cosmix.Application.Finance.Ledger.FinanceLedgerEntry>()
                .Where(x => x.JournalId == salaryJournal.Id).ToArrayAsync();
            Assert.Equal(2, legs.Length);
            Assert.Equal(1000m, legs.Sum(x => x.Debit)); Assert.Equal(1000m, legs.Sum(x => x.Credit));
            Assert.All(legs, leg => Assert.Equal(obligation.Id, leg.ObligationId));
            var posting = new StatutoryEmployeePosting(entry.Id, contribution.Id, obligation.Id, null,
                new('A', 64), "STAT", "independent-finance", now);
            var accounting = new PayrollAccountingRepository(db);
            await accounting.AddContributionPostingAsync(posting, default); await db.SaveChangesAsync();
            Assert.Equal(entry.Id, Assert.Single(await accounting.ContributionPostingsAsync(contribution.Id, default)).EntryId);
            var cost = new PayrollCostPlan(Guid.NewGuid(), batch.Id, new('A', 64), 1, null,
                "synthetic-cost-proof", "cost-maker", employee, now);
            db.Add(cost); await db.SaveChangesAsync();
            var costRepository = new Cosmix.Infrastructure.Finance.PayrollCostRepository(db);
            var costVersion = cost.RowVersion.ToArray();
            cost.Decide(true, "independent-cost-reviewer", Guid.NewGuid(), now, "synthetic-cost-decision");
            costRepository.ExpectVersion(cost, new byte[8]);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
            costRepository.ExpectVersion(cost, costVersion); await db.SaveChangesAsync();
            Assert.Equal("Approved", Assert.Single(await costRepository.PlansAsync(batch.Id, default)).State);
            db.Entry(cost).Property(x => x.ProofReference).CurrentValue = "changed-proof";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.Entry(cost).State = EntityState.Detached;
            db.Entry(posting).State = EntityState.Modified;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
            db.Entry(posting).State = EntityState.Detached;
            await accounting.AddContributionPostingAsync(new(entry.Id, contribution.Id, obligation.Id, null,
                new('A', 64), "STAT", "second-finance", now), default);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            contributionId = contribution.Id; entryId = entry.Id; auditId = audit.Id;
            // A later authorization/audit failure must be able to roll every earlier repository save back.
            await transaction.RollbackAsync();
        }
        await using var independent = Context();
        Assert.False(await independent.StatutoryContributions.AsNoTracking().AnyAsync(x => x.Id == contributionId));
        Assert.False(await independent.Set<StatutoryContributionEntry>().AsNoTracking().AnyAsync(x => x.Id == entryId));
        Assert.False(await independent.Set<StatutoryEmployeePosting>().AsNoTracking().AnyAsync(x => x.EntryId == entryId));
        Assert.False(await independent.AuditEvents.AsNoTracking().AnyAsync(x => x.Id == auditId));
    }

    [PayrollSqlFact]
    public async Task Posting_locks_require_and_preserve_an_enclosing_SQL_transaction()
    {
        await using var db = Context();
        var statutory = new StatutoryContributionRepository(db);
        var accounting = new PayrollAccountingRepository(db);
        var contribution = Guid.NewGuid(); var obligation = Guid.NewGuid(); var employee = Guid.NewGuid();
        Func<Task>[] locks = [
            () => statutory.AcquireStatutoryPostingLockAsync(contribution, default),
            () => accounting.AcquireStatutoryPostingLockAsync(contribution, default),
            () => accounting.AcquirePaymentLockAsync(obligation, employee, default)
        ];
        foreach (var acquire in locks)
        {
            var error = await Assert.ThrowsAsync<SqlException>(acquire);
            Assert.Equal(51004, error.Number);
            Assert.Null(db.Database.CurrentTransaction);
        }
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var acquire in locks)
        {
            await acquire();
            Assert.Same(transaction, db.Database.CurrentTransaction);
        }
        await transaction.RollbackAsync();
    }

    [PayrollSqlFact]
    public async Task Migrations_and_model_are_current_and_actual_queries_translate()
    {
        await using var db = Context();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        var employee = await db.Employees.AsNoTracking().Select(x => x.Id).FirstAsync();
        var slips = new PayslipRepository(db);
        Assert.NotNull(await slips.OwnBatchIdsAsync(employee, 1, 20, default));
        Assert.False(await slips.ContainsEmployeeAsync(Guid.NewGuid(), employee, default));
        Assert.NotNull(await new PayrollImportRepository(db).HistoryAsync(1, 25, default));
        Assert.NotNull(await new PayrollSalaryRepository(db).ComponentsAsync(employee, default));
        Assert.NotNull(await new PayrollSalaryRepository(db).PoliciesAsync(default));
        Assert.NotNull(await new PayrollAccountingRepository(db).ListSettlementBatchesAsync(1, 25, default));
        _ = await db.Set<StatutoryContributionEntry>().AsNoTrackingWithIdentityResolution()
            .Include(x => x.Contribution).ThenInclude(x => x.Entries).Take(1).ToArrayAsync();
    }

    [PayrollSqlFact]
    public async Task Import_source_and_submission_roundtrip_and_duplicate_hash_is_rejected()
    {
        await using var db = Context();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var id = Guid.NewGuid(); var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(id.ToByteArray()));
        var now = DateTimeOffset.UtcNow;
        var source = new PayrollSalaryImport(id, "synthetic.csv", hash, "synthetic-private-source", "synthetic-maker", now);
        db.Add(source); await db.SaveChangesAsync();
        Assert.Equal(8, source.RowVersion.Length);
        source.Submit(new('B', 64), "synthetic-private-result", "synthetic-maker", now.AddSeconds(1));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var stored = await new PayrollImportRepository(db).FindAsync(id, false, default);
        Assert.Equal(source.SubmittedAt, stored!.SubmittedAt); Assert.Equal(hash, stored.SourceSha256);
        db.Add(new PayrollSalaryImport(Guid.NewGuid(), "duplicate.csv", hash, "private", "synthetic-maker", now));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [PayrollSqlFact]
    public async Task Complete_salary_metadata_persists_instead_of_reverting_to_legacy()
    {
        await using var db = Context();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var employee = await db.Employees.AsNoTracking().Select(x => x.Id).FirstAsync();
        var period = new AttendancePeriodLock(new(2098, 1, 1), new(2098, 2, 1));
        var locked = period.RecordDirectLock(Guid.NewGuid(), "synthetic-maker", "SQL persistence fixture",
            DateTimeOffset.UtcNow, "synthetic-private-month-lock");
        var summary = new DailyAttendanceSummary(employee, period.PeriodStart, 1, AttendanceDayStatus.Unverified,
            null, null, "synthetic-sql", new('A', 64), 0, 0, null, null, null, 0, DateTimeOffset.UtcNow);
        db.AddRange(period, locked, summary, new AttendancePeriodSummary(period, locked, summary));
        var batch = new PayrollHandoffBatch("SQL-" + Guid.NewGuid().ToString("N"), new(2098, 1, 1), new(2098, 2, 1),
            "PKR", "synthetic-maker", DateTimeOffset.UtcNow);
        batch.Lines.Add(new(batch.Id, employee, PayrollComponentKind.BaseCompensation,
            "attendance.payroll-salary.base", Guid.NewGuid(), 1, locked.Id, 1000m, "PKR", null, null,
            "synthetic-private-line", "SQL-" + Guid.NewGuid().ToString("N")));
        batch.SetSalaryEvidence(new('A', 64), new('B', 64), "synthetic-private-calculation");
        db.Add(batch); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var stored = await db.PayrollHandoffBatches.Include(x => x.Lines).SingleAsync(x => x.Id == batch.Id);
        Assert.Equal("salary-v2", stored.CalculationVersion); Assert.Equal("prepared", stored.ReviewState);
        Assert.Equal(batch.FrozenFingerprint, stored.FrozenFingerprint);
        Assert.Equal(batch.CalculationPrivateReference, stored.CalculationPrivateReference);
        Assert.Equal(8, stored.RowVersion.Length);
        Assert.Equal(1000m, Assert.Single(stored.Lines).Amount);
        stored.Lines.Add(new(stored.Id, employee, PayrollComponentKind.SalaryAdjustment,
            "synthetic-forbidden-append", Guid.NewGuid(), 1, locked.Id, 100m, "PKR", null, null,
            "synthetic-private-line", "SQL-" + Guid.NewGuid().ToString("N")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }

    [PayrollSqlFact]
    public async Task Actual_statutory_entries_and_approval_rowversion_roundtrip()
    {
        await using var db = Context();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var employee = await db.Employees.AsNoTracking().Select(x => x.Id).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var registration = await db.EmployeeStatutoryRegistrations.FirstOrDefaultAsync(x => x.EmployeeId == employee && x.Scheme == StatutoryScheme.EOBI);
        if (registration is null)
        {
            registration = new(employee, StatutoryScheme.EOBI, "SYNTHETIC-" + Guid.NewGuid().ToString("N"), new(2098, 1, 1), "maker", now);
            db.Add(registration);
        }
        var contribution = new StatutoryContribution(StatutoryScheme.EOBI, new(2098, 1, 1), 100, 500, "PKR", "maker", now, hasEmployeeEntries: true);
        contribution.Entries.Add(new(contribution.Id, employee, registration.Id, registration.RegistrationNumber, 100, 500,
            "synthetic-source", new('A', 64), "synthetic-private-proof"));
        db.Add(contribution); await db.SaveChangesAsync();
        var before = Convert.ToBase64String(contribution.RowVersion);
        contribution.Approve("independent-reviewer", now.AddSeconds(1)); await db.SaveChangesAsync();
        Assert.NotEqual(before, Convert.ToBase64String(contribution.RowVersion));
        db.ChangeTracker.Clear();
        var stored = await db.StatutoryContributions.Include(x => x.Entries).SingleAsync(x => x.Id == contribution.Id);
        var entry = Assert.Single(stored.Entries);
        Assert.Equal(100, entry.EmployeeContributionAmount); Assert.Equal(500, entry.EmployerContributionAmount);
        Assert.Equal(ContributionState.Approved, stored.State);
        await transaction.RollbackAsync();
    }
}
