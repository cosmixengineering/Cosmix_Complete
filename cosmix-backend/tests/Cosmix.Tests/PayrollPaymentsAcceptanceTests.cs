using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollAccounting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollPaymentsAcceptanceFactAttribute : FactAttribute
{
    public PayrollPaymentsAcceptanceFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_PAYROLL_PAYMENTS_CONNECTION")))
            Skip = "Requires the exact prepared Cosmix_Payroll_Payments_20261028 clone and copied private evidence. Never targets the original month.";
    }
}

public sealed class PayrollPaymentsAcceptanceTests
{
    private const string Reason = "SYNTHETIC payment continuation only; no bank transfer or company acceptance";
    private const string Evidence = "SYNTHETIC retained statement/receipt fixture, not a bank-issued payment confirmation";
    private static readonly DateOnly PostingDate = new(2026,10,28);

    [PayrollPaymentsAcceptanceFact]
    public async Task Cloned_month_partial_confirmation_failure_reversal_replacement_and_employee_reports_reconcile_in_SQL()
    {
        await using var f = new PayrollPaymentsAcceptanceFixture();
        await f.LoadAsync();
        var context = PayrollPaymentsAcceptanceFixture.Context;
        var checks = new List<string>();
        var baseline = await f.Db.PayrollJournalEntries.AsNoTracking().Where(x => x.ObligationId == f.Obligation.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Id, x.ObligationId, x.Kind, x.DebitAccountCode, x.CreditAccountCode,
                x.Amount, x.Currency, x.PostedAt, x.PostedByUserId, x.SourceKey, x.PostingDate, x.AccountingPeriodId, x.PostingProfileId }).ToArrayAsync();
        var originalIds = baseline.Select(x => x.Id).ToArray();
        var baselineJson = JsonSerializer.Serialize(baseline);
        var protectedBefore = Directory.GetFiles(Path.Combine(PayrollPaymentsAcceptanceFixture.EvidenceRoot,"private","objects"),"*",SearchOption.AllDirectories)
            .ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        f.Actor(f.Maker);
        var initial = await f.Reports.ReadAsync(f.Obligation.BatchId,context,default);
        Assert.Equal(32200m, initial.OutstandingPayments); Assert.Equal(0, initial.PaymentLedgerNet); Assert.Empty(initial.Issues);
        checks.Add("Preserved approved salary/frozen proofs read from clone; initial four journals and full unpaid entitlement agree.");

        await f.Setup.CreateAccountAsync(new("TEST-PMT-BANK","Synthetic payment bank ledger",PayrollAccountKind.Bank,Reason),"payment-clone-bank",context,default);
        RecordSalaryPaymentRequest Request(decimal amount,string reference,DateOnly? date=null) => new(f.Obligation.Id,f.Accrual.Id,reference,
            "TEST-PAY","TEST-PMT-BANK",amount,f.EmployeeId,date ?? PostingDate,Reason);
        var closed = await Assert.ThrowsAsync<AuthenticationFailure>(()=>f.Accounting.RecordSalaryPaymentAsync(Request(1,"SYNTHETIC-CLOSED",new(2026,9,30)),
            "payment-clone-closed-period",context,default));
        Assert.Equal("payroll_accounting_period_not_open",closed.Code);
        Assert.Empty(await f.Db.SalaryPayments.AsNoTracking().ToArrayAsync());
        var period = await f.Setup.CreatePeriodAsync(new(new(2026,10,1),new(2026,11,1),"PKR",Reason),"payment-clone-period-create",context,default);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(()=>f.Setup.DecidePeriodAsync(period.Id,new(period.Version,Reason),false,
            "payment-clone-period-self-denied",context,default));
        f.Actor(f.Reviewer);
        period = await f.Setup.DecidePeriodAsync(period.Id,new(period.Version,Reason),false,"payment-clone-period-approve",context,default);
        Assert.Equal("Open",period.State);
        checks.Add("Closed original September period rejected payment; separate October period independently approved through actual setup service.");

        f.Actor(f.Maker);
        var firstRequest=Request(12000,"SYNTHETIC-PARTIAL-1");
        var first=await f.Accounting.RecordSalaryPaymentAsync(firstRequest,"payment-clone-first",context,default);
        var replay=await f.Accounting.RecordSalaryPaymentAsync(firstRequest,"payment-clone-first",context,default);
        Assert.Equal(first,replay); Assert.Equal("Posted",first.State);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(()=>f.Accounting.RecordSalaryPaymentAsync(firstRequest,
            "payment-clone-duplicate-reference",context,default));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(()=>f.Accounting.RecordSalaryPaymentAsync(Request(20201,"SYNTHETIC-TOO-MUCH"),
            "payment-clone-overpay",context,default));
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(()=>f.Accounting.ConfirmPaymentAsync(first.Id,new(Reason,Evidence,first.Version),
            "payment-clone-self-confirm",context,default));
        f.Actor(f.Reviewer);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(()=>f.Accounting.ConfirmPaymentAsync(first.Id,new(Reason,null,first.Version),
            "payment-clone-empty-proof",context,default));
        var confirmed=await f.Accounting.ConfirmPaymentAsync(first.Id,new(Reason,Evidence,first.Version),"payment-clone-first-confirm",context,default);
        Assert.Equal("Confirmed",confirmed.State);
        checks.Add("First 12000 payment posts once with stable replay; duplicate reference, overpayment, maker confirmation and missing proof rejected; independent retained confirmation succeeds.");

        f.Actor(f.Payee);
        var partial=await f.Own.PaymentPeriodAsync(f.Obligation.BatchId,1,20,context,default);
        Assert.Equal("PartiallyPaid",partial.PaymentState);Assert.Equal(12000m,partial.ConfirmedAmount);Assert.Equal(20200m,partial.OutstandingAmount);
        Assert.Equal("PartiallyPaid",Assert.Single((await f.Own.PayslipsAsync(1,20,context,default)).Items).PaymentState);
        checks.Add("Employee payment history and own payslip show the same partial payment and 20200 outstanding.");

        f.Actor(f.Maker);
        var failedPosted=await f.Accounting.RecordSalaryPaymentAsync(Request(5000,"SYNTHETIC-FAILED-ATTEMPT"),"payment-clone-failed-post",context,default);
        var failedRequested=await f.Accounting.RequestPaymentReversalAsync(failedPosted.Id,new(Reason,Evidence,true,failedPosted.Version),
            "payment-clone-failure-request",context,default);
        await Assert.ThrowsAsync<PayrollFinanceConflictException>(()=>f.Accounting.ApprovePaymentReversalAsync(failedRequested.Id,
            new(Reason,Evidence,PostingDate,failedRequested.Version),"payment-clone-failure-self-approve",context,default));
        f.Actor(f.ReversalReviewer);
        var failed=await f.Accounting.ApprovePaymentReversalAsync(failedRequested.Id,new(Reason,Evidence,PostingDate,failedRequested.Version),
            "payment-clone-failure-approve",context,default);
        Assert.Equal("Failed",failed.State);Assert.NotNull(failed.ReversalJournalEntryId);
        var failedReplay=await f.Accounting.ApprovePaymentReversalAsync(failedRequested.Id,new(Reason,Evidence,PostingDate,failedRequested.Version),
            "payment-clone-failure-approve",context,default);
        Assert.Equal(failed,failedReplay);
        checks.Add("Failed posted attempt has an independent exact reversal journal; self-approval rejected and approval replay creates no second reversal.");

        f.Actor(f.Maker);
        var remainder=await f.Accounting.RecordSalaryPaymentAsync(Request(20200,"SYNTHETIC-REMAINDER"),"payment-clone-remainder",context,default);
        f.Actor(f.Reviewer);
        remainder=await f.Accounting.ConfirmPaymentAsync(remainder.Id,new(Reason,Evidence,remainder.Version),"payment-clone-remainder-confirm",context,default);
        Assert.Equal("Paid",(await f.Reports.ReadAsync(f.Obligation.BatchId,context,default)).Employees.Single().Payments.State);

        f.Actor(f.Maker);
        var reversalRequested=await f.Accounting.RequestPaymentReversalAsync(confirmed.Id,new(Reason,Evidence,false,confirmed.Version),
            "payment-clone-confirmed-reversal-request",context,default);
        f.Actor(f.ReversalReviewer);
        var reversed=await f.Accounting.ApprovePaymentReversalAsync(confirmed.Id,new(Reason,Evidence,PostingDate,reversalRequested.Version),
            "payment-clone-confirmed-reversal-approve",context,default);
        Assert.Equal("Reversed",reversed.State);
        f.Actor(f.Payee);
        var restored=await f.Own.PaymentPeriodAsync(f.Obligation.BatchId,1,1,context,default);
        Assert.Equal(20200m,restored.ConfirmedAmount);Assert.Equal(12000m,restored.OutstandingAmount);
        Assert.Equal(12000m,restored.ReversedAmount);Assert.Equal(5000m,restored.FailedAmount);Assert.True(restored.HasMorePayments);
        Assert.Single(restored.Payments);
        checks.Add("Reversing an independently confirmed payment restores exactly 12000 entitlement; one-item history pagination retains all-payment totals and failed/reversed history.");

        f.Actor(f.Maker);
        var replacement=await f.Accounting.RecordSalaryPaymentAsync(Request(12000,"SYNTHETIC-REPLACEMENT"),"payment-clone-replacement",context,default);
        f.Actor(f.Reviewer);
        replacement=await f.Accounting.ConfirmPaymentAsync(replacement.Id,new(Reason,Evidence,replacement.Version),"payment-clone-replacement-confirm",context,default);
        var report=await f.Reports.ReadAsync(f.Obligation.BatchId,context,default);
        Assert.Equal(32200m,report.ConfirmedPayments);Assert.Equal(32200m,report.PaymentLedgerNet);
        Assert.Equal(0,report.OutstandingPayments);Assert.Equal(0,report.ReservedPayments);Assert.Equal(0,report.PaymentLedgerDifference);Assert.Empty(report.Issues);
        Assert.Equal("Paid",Assert.Single(report.Employees).Payments.State);
        f.RemovePermission("payroll.reports.read");
        await Assert.ThrowsAsync<AuthenticationFailure>(()=>f.Reports.ReadAsync(f.Obligation.BatchId,context,default));
        f.AddPermission("payroll.reports.read");
        var csv=await f.Reports.ExportAsync(f.Obligation.BatchId,"csv",context,default);
        var csvBytes=Convert.FromBase64String(csv.Base64);Assert.Equal(csv.Sha256,Convert.ToHexString(SHA256.HashData(csvBytes)));
        var csvText=Encoding.UTF8.GetString(csvBytes);Assert.Contains("32200.00",csvText);Assert.DoesNotContain("SYNTHETIC-REPLACEMENT",csvText);
        await PayrollPaymentsAcceptanceFixture.SaveArtifactAsync("SYNTHETIC-settlement.csv",csvBytes);
        checks.Add("Replacement payment settles exactly 32200; SQL-backed report reconciles all postings/reversals and denies a revoked report permission; CSV hash verified without bank/receipt references.");

        f.Actor(f.Payee);
        var own=await f.Own.PaymentsAsync(1,20,context,default);var ownPeriod=Assert.Single(own.Items);
        Assert.Equal("Paid",ownPeriod.PaymentState);Assert.Equal(32200m,ownPeriod.ConfirmedAmount);Assert.Equal(0,ownPeriod.OutstandingAmount);
        var serialized=JsonSerializer.Serialize(own);
        Assert.DoesNotContain("SYNTHETIC-REPLACEMENT",serialized);Assert.DoesNotContain("ProofReference",serialized);Assert.DoesNotContain("BankExport",serialized);
        Assert.Equal("Paid",Assert.Single((await f.Own.PayslipsAsync(1,20,context,default)).Items).PaymentState);
        var payslip=await f.Own.PayslipAsync(f.Obligation.BatchId,"xlsx",context,default);
        var payslipBytes=Convert.FromBase64String(payslip.Base64);Assert.Equal(payslip.Sha256,Convert.ToHexString(SHA256.HashData(payslipBytes)));
        using(var zip=new ZipArchive(new MemoryStream(payslipBytes)))
        {
            var xml=string.Join("\n",zip.Entries.Where(x=>x.FullName.EndsWith(".xml")).Select(x=>{using var reader=new StreamReader(x.Open());return reader.ReadToEnd();}));
            Assert.Contains("Paid",xml);Assert.Contains("32200.00",xml);
        }
        await PayrollPaymentsAcceptanceFixture.SaveArtifactAsync("SYNTHETIC-paid-payslip.xlsx",payslipBytes);
        f.ActorEmployeeId=Guid.NewGuid();
        var denied=await Assert.ThrowsAsync<AuthenticationFailure>(()=>f.Own.PaymentPeriodAsync(f.Obligation.BatchId,1,20,context,default));
        Assert.Equal("payment_history_not_found",denied.Code);f.ActorEmployeeId=f.EmployeeId;
        checks.Add("Own payment page, period and protected payslip agree Paid; foreign employee identity denied before exposure; employee JSON excludes bank references and proof locators.");

        var persisted=await f.Db.SalaryPayments.AsNoTracking().Where(x=>x.ObligationId==f.Obligation.Id).ToArrayAsync();
        Assert.Equal(4,persisted.Length);Assert.Equal(2,persisted.Count(x=>x.State==SalaryPaymentState.Confirmed));
        Assert.Single(persisted,x=>x.State==SalaryPaymentState.Failed);Assert.Single(persisted,x=>x.State==SalaryPaymentState.Reversed);
        var allJournals=await f.Db.PayrollJournalEntries.AsNoTracking().Where(x=>x.ObligationId==f.Obligation.Id).ToArrayAsync();
        Assert.Equal(10,allJournals.Length);Assert.Equal(49200m,allJournals.Where(x=>x.Kind==JournalEntryKind.SalaryPayment).Sum(x=>x.Amount));
        Assert.Equal(17000m,allJournals.Where(x=>x.Kind==JournalEntryKind.SalaryPaymentReversal).Sum(x=>x.Amount));
        Assert.Equal(100700m,allJournals.Sum(x=>x.Amount));
        Assert.Equal(allJournals.Length,allJournals.Select(x=>x.SourceKey).Distinct().Count());
        var after=await f.Db.PayrollJournalEntries.AsNoTracking().Where(x=>originalIds.Contains(x.Id)).OrderBy(x=>x.Id)
            .Select(x=>new{x.Id,x.ObligationId,x.Kind,x.DebitAccountCode,x.CreditAccountCode,x.Amount,x.Currency,x.PostedAt,x.PostedByUserId,x.SourceKey,x.PostingDate,x.AccountingPeriodId,x.PostingProfileId}).ToArrayAsync();
        Assert.Equal(baselineJson,JsonSerializer.Serialize(after));
        var frozen=await f.Frozen.ReadAsync(f.Obligation.BatchId,default);Assert.Equal(f.Obligation.SourceFingerprint,frozen.Fingerprint);
        Assert.Equal(30,await f.Db.DailyAttendanceSummaries.CountAsync(x=>x.EmployeeId==f.EmployeeId));
        foreach(var item in protectedBefore) Assert.Equal(item.Value,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.Key))));
        checks.Add("Persisted SQL has four payment attempts, four payment journals and two exact reversal journals; ledger net 32200. Original four accrual/contribution journals, 30 attendance days, frozen salary fingerprint and all copied prior encrypted objects remain byte/value unchanged.");
        await f.SaveEvidenceAsync("payment-acceptance-result.json",new{
            Scenario="SYNTHETIC cloned September payroll paid/reversed/replaced in October; no external bank transfer",
            Database="Cosmix_Payroll_Payments_20261028",f.EmployeeId,BatchId=f.Obligation.BatchId,f.Obligation.SourceFingerprint,
            Gross=33000m,Deductions=800m,Net=32200m,EffectiveConfirmed=32200m,HistoricalFailed=5000m,HistoricalReversed=12000m,
            PaymentAttempts=persisted.Length,JournalCount=allJournals.Length,TotalDebits=allJournals.Sum(x=>x.Amount),TotalCredits=allJournals.Sum(x=>x.Amount),
            PaymentLedgerNet=report.PaymentLedgerNet,report.PaymentLedgerDifference,Checks=checks,OwnPaymentPeriod=ownPeriod,
            OriginalEvidenceUnchanged=true,ExternalTransferPerformed=false,RealLoginOrPhysicalDeviceAcceptance=false,CompanySignoff=false});
        await f.SaveEvidenceAsync("settlement-report.json",report);
    }
}
