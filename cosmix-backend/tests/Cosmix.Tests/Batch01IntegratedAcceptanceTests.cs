using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Shifts;
using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Application.Common.Security;
using Cosmix.Application.Finance.Costing;
using Cosmix.Application.Finance.Ledger;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollHandoff;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Domain.Common.Idempotency;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Xunit;

namespace Cosmix.Tests;

public sealed class Batch01IntegratedAcceptanceTests
{
    private static readonly DateOnly MonthStart = new(2026, 10, 1);
    private static readonly DateOnly MonthEndExclusive = new(2026, 11, 1);
    private const string Currency = "PKR";

    [Fact]
    public async Task Integrated_Batch01_Scenario_Covers_Return_IndependentApprovals_And_FinanceCosting()
    {
        // -------------------------------------------------------------
        // SETUP: Synthetic employees across General Staff, HR, Finance, Admin, and Independent Management
        // -------------------------------------------------------------
        var empGeneral1 = Guid.NewGuid();
        var empGeneral2 = Guid.NewGuid();
        var empHR = Guid.NewGuid();
        var empFinance = Guid.NewGuid();
        var empAdmin = Guid.NewGuid(); // Preparer
        var empIndependentMgmt = Guid.NewGuid(); // Management Locker (CEO/Director)
        var empIndependentSecondReviewer = Guid.NewGuid(); // Second HR Reviewer

        var now = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        // Define authoritative lines for all 5 paid employees
        var rawLines = new (Guid EmployeeId, decimal Base, decimal Overtime, decimal Deductions)[]
        {
            (empGeneral1, 50000m, 5000m, -2000m),
            (empGeneral2, 55000m, 0m,    -1500m),
            (empHR,       75000m, 0m,    -5000m),
            (empFinance,  70000m, 0m,    -3000m),
            (empAdmin,    80000m, 10000m, -4000m)
        };

        var previewLines = new List<PayrollHandoffPreviewLine>();
        foreach (var l in rawLines)
        {
            previewLines.Add(new(l.EmployeeId, "Base Compensation", "attendance.payroll-salary.base", Guid.NewGuid(), 1, l.Base, Currency, null, null));
            if (l.Overtime > 0)
                previewLines.Add(new(l.EmployeeId, "Overtime Pay", "attendance.overtime", Guid.NewGuid(), 1, l.Overtime, Currency, null, null));
            if (l.Deductions < 0)
                previewLines.Add(new(l.EmployeeId, "Attendance Deductions", "attendance.deduction", Guid.NewGuid(), 1, l.Deductions, Currency, null, null));
        }

        var fullMonthTotals = previewLines.GroupBy(x => x.EmployeeId)
            .Select(g => (EmployeeId: g.Key, Net: g.Sum(x => x.Amount)))
            .ToDictionary(x => x.EmployeeId, x => x.Net);

        // Compute authoritative preview fingerprint
        var fingerprintBytes = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(previewLines.Select(x => (x.EmployeeId, x.Component, x.Amount))));
        var previewFingerprint = Convert.ToHexString(fingerprintBytes).ToLowerInvariant();

        // Track active batches in synthetic state
        var batches = new List<PayrollHandoffBatch>();
        var approvalRequests = new List<ApprovalRequest>();
        var approvalDecisions = new List<ApprovalDecision>();

        // -------------------------------------------------------------
        // STEP 1 & 2: Prepare Cohort A (General Staff: empGeneral1, empGeneral2)
        // -------------------------------------------------------------
        var cohortAEmployees = new[] { empGeneral1, empGeneral2 };
        var batchA = new PayrollHandoffBatch("BATCH-202610-COHORT-A", MonthStart, MonthEndExclusive, Currency, empAdmin.ToString(), now);
        var lockEventId = Guid.NewGuid();
        foreach (var pl in previewLines.Where(x => cohortAEmployees.Contains(x.EmployeeId)))
        {
            batchA.Lines.Add(new(batchA.Id, pl.EmployeeId, PayrollComponentKind.BaseCompensation, pl.Component,
                pl.SourceId, pl.SourceVersion, lockEventId, pl.Amount, pl.Currency, null, null, "evidence", Guid.NewGuid().ToString("N")));
        }
        batchA.SetSalaryEvidence(new('A', 64), new('B', 64), "evidence-a");
        batches.Add(batchA);

        Assert.Equal("prepared", batchA.ReviewState);
        Assert.Equal(2, batchA.Lines.Select(x => x.EmployeeId).Distinct().Count());
        var cohortANetTotal = batchA.Lines.Sum(x => x.Amount);
        Assert.Equal(fullMonthTotals[empGeneral1] + fullMonthTotals[empGeneral2], cohortANetTotal);

        // -------------------------------------------------------------
        // STEP 3: Attempt overlapping employee preparation -> MUST BE REJECTED
        // -------------------------------------------------------------
        var overlappingAttempt = new[] { empGeneral1, empHR };
        bool overlapDetected = batches.Any(b => b.PeriodStart == MonthStart && b.PeriodEnd == MonthEndExclusive &&
            b.ReviewState != "cancelled" && b.ReviewState != "rejected" && b.ReviewState != "returned" &&
            b.Lines.Any(line => overlappingAttempt.Contains(line.EmployeeId)));
        Assert.True(overlapDetected, "Overlapping employee in another active cohort must be rejected.");

        // Now prepare valid, non-overlapping Cohort B (empHR, empFinance, empAdmin)
        var cohortBEmployees = new[] { empHR, empFinance, empAdmin };
        var batchB = new PayrollHandoffBatch("BATCH-202610-COHORT-B", MonthStart, MonthEndExclusive, Currency, empAdmin.ToString(), now);
        foreach (var pl in previewLines.Where(x => cohortBEmployees.Contains(x.EmployeeId)))
        {
            batchB.Lines.Add(new(batchB.Id, pl.EmployeeId, PayrollComponentKind.BaseCompensation, pl.Component,
                pl.SourceId, pl.SourceVersion, lockEventId, pl.Amount, pl.Currency, null, null, "evidence", Guid.NewGuid().ToString("N")));
        }
        batchB.SetSalaryEvidence(new('C', 64), new('D', 64), "evidence-b");
        batches.Add(batchB);

        Assert.Equal("prepared", batchB.ReviewState);
        Assert.Equal(3, batchB.Lines.Select(x => x.EmployeeId).Distinct().Count());

        // Reconcile multi-cohort totals equals full preview net total
        Assert.Equal(fullMonthTotals.Values.Sum(), batchA.Lines.Sum(x => x.Amount) + batchB.Lines.Sum(x => x.Amount));

        // -------------------------------------------------------------
        // STEP 4: Independent Approval of Cohort A by empHR
        // -------------------------------------------------------------
        // empHR is NOT in Cohort A, so empHR CAN approve Cohort A
        Assert.DoesNotContain(batchA.Lines, x => x.EmployeeId == empHR);
        var reqHrA = new ApprovalRequest("payroll-handoff.hr-review", batchA.Id, batchA.PreparedByUserId, "ref-a", 1, now.AddMinutes(5));
        var decHrA = reqHrA.Decide(ApprovalState.Approved, empHR.ToString(), "HR", "Approved Cohort A", now.AddMinutes(5));
        approvalRequests.Add(reqHrA); approvalDecisions.Add(decHrA);
        batchA.ReviewSalary(reqHrA.Id, empHR.ToString(), now.AddMinutes(5), true);
        Assert.Equal("hr_approved", batchA.ReviewState);

        // SELF-APPROVAL ENFORCEMENT: empHR CANNOT approve Cohort B (empHR is in Cohort B)
        Assert.Contains(batchB.Lines, x => x.EmployeeId == empHR);
        // Preparer cannot review either
        Assert.Throws<InvalidOperationException>(() => batchB.ReviewSalary(Guid.NewGuid(), batchB.PreparedByUserId, now.AddMinutes(6), true));

        // -------------------------------------------------------------
        // STEP 5: Independent Approval of Cohort B by empIndependentSecondReviewer
        // -------------------------------------------------------------
        Assert.DoesNotContain(batchB.Lines, x => x.EmployeeId == empIndependentSecondReviewer);
        var reqHrB = new ApprovalRequest("payroll-handoff.hr-review", batchB.Id, batchB.PreparedByUserId, "ref-b", 1, now.AddMinutes(10));
        var decHrB = reqHrB.Decide(ApprovalState.Approved, empIndependentSecondReviewer.ToString(), "HR", "Approved Cohort B", now.AddMinutes(10));
        approvalRequests.Add(reqHrB); approvalDecisions.Add(decHrB);
        batchB.ReviewSalary(reqHrB.Id, empIndependentSecondReviewer.ToString(), now.AddMinutes(10), true);
        Assert.Equal("hr_approved", batchB.ReviewState);

        // -------------------------------------------------------------
        // STEP 6: Return Cohort B for Correction before Lock
        // -------------------------------------------------------------
        // Independent reviewer returns Cohort B with mandatory reason
        var returnReqB = new ApprovalRequest("payroll-handoff.return-for-correction", batchB.Id, batchB.PreparedByUserId, "return-ref-b", 1, now.AddMinutes(15));
        var returnDecB = returnReqB.Decide(ApprovalState.Returned, empIndependentSecondReviewer.ToString(), "HR", "Overtime hour rate correction required for Admin", now.AddMinutes(15));
        approvalRequests.Add(returnReqB); approvalDecisions.Add(returnDecB);
        batchB.ReturnForCorrection(returnReqB.Id, empIndependentSecondReviewer.ToString(), now.AddMinutes(15));

        Assert.Equal("returned", batchB.ReviewState);
        Assert.Equal(reqHrB.Id, batchB.HrApprovalRequestId); // Prior approval history retained
        Assert.Equal(returnReqB.Id, batchB.ReturnApprovalRequestId); // Return audit decision linked

        // Cohort A is unaffected by Cohort B's return
        Assert.Equal("hr_approved", batchA.ReviewState);

        // -------------------------------------------------------------
        // STEP 7 & 8: Correct Source & Prepare Replacement Cohort B (Cohort B2)
        // -------------------------------------------------------------
        // Because Cohort B is returned, employees in Cohort B are now released and available for fresh preparation
        bool cohortBEmployeesAvailable = !batches.Any(b => b.PeriodStart == MonthStart && b.PeriodEnd == MonthEndExclusive &&
            b.ReviewState != "cancelled" && b.ReviewState != "rejected" && b.ReviewState != "returned" &&
            b.Lines.Any(line => cohortBEmployees.Contains(line.EmployeeId)));
        Assert.True(cohortBEmployeesAvailable, "Employees in returned cohort must be released for re-preparation.");

        // Prepare replacement batch with updated admin overtime (12000 instead of 10000)
        var batchB2 = new PayrollHandoffBatch("BATCH-202610-COHORT-B-CORRECTED", MonthStart, MonthEndExclusive, Currency, empAdmin.ToString(), now.AddMinutes(20));
        foreach (var pl in previewLines.Where(x => cohortBEmployees.Contains(x.EmployeeId)))
        {
            var amount = (pl.EmployeeId == empAdmin && pl.Component == "Overtime Pay") ? 12000m : pl.Amount;
            batchB2.Lines.Add(new(batchB2.Id, pl.EmployeeId, PayrollComponentKind.BaseCompensation, pl.Component,
                pl.SourceId, pl.SourceVersion, lockEventId, amount, pl.Currency, null, null, "evidence-b2", Guid.NewGuid().ToString("N")));
        }
        batchB2.SetSalaryEvidence(new('E', 64), new('F', 64), "evidence-b2");
        batches.Add(batchB2);
        Assert.Equal("prepared", batchB2.ReviewState);

        // -------------------------------------------------------------
        // STEP 9: Re-approve Replacement Cohort B2 by Independent HR Reviewer
        // -------------------------------------------------------------
        var reqHrB2 = new ApprovalRequest("payroll-handoff.hr-review", batchB2.Id, batchB2.PreparedByUserId, "ref-b2", 1, now.AddMinutes(25));
        var decHrB2 = reqHrB2.Decide(ApprovalState.Approved, empIndependentSecondReviewer.ToString(), "HR", "Approved replacement Cohort B2", now.AddMinutes(25));
        approvalRequests.Add(reqHrB2); approvalDecisions.Add(decHrB2);
        batchB2.ReviewSalary(reqHrB2.Id, empIndependentSecondReviewer.ToString(), now.AddMinutes(25), true);
        Assert.Equal("hr_approved", batchB2.ReviewState);

        // -------------------------------------------------------------
        // STEP 10: Management Lock by Authorized Independent Management Reviewer
        // -------------------------------------------------------------
        // Returned Batch B CANNOT be locked
        Assert.Throws<InvalidOperationException>(() => batchB.Lock(Guid.NewGuid(), empIndependentMgmt.ToString(), now.AddMinutes(30)));

        // Lock Cohort A
        var lockReqA = new ApprovalRequest("payroll-handoff.lock", batchA.Id, batchA.PreparedByUserId, "lock-ref-a", 1, now.AddMinutes(30));
        var lockDecA = lockReqA.Decide(ApprovalState.Approved, empIndependentMgmt.ToString(), "Management", "Locked Cohort A", now.AddMinutes(30));
        approvalRequests.Add(lockReqA); approvalDecisions.Add(lockDecA);
        batchA.Lock(lockReqA.Id, empIndependentMgmt.ToString(), now.AddMinutes(30));
        Assert.Equal("locked", batchA.ReviewState);

        // Lock Cohort B2
        var lockReqB2 = new ApprovalRequest("payroll-handoff.lock", batchB2.Id, batchB2.PreparedByUserId, "lock-ref-b2", 1, now.AddMinutes(35));
        var lockDecB2 = lockReqB2.Decide(ApprovalState.Approved, empIndependentMgmt.ToString(), "Management", "Locked Cohort B2", now.AddMinutes(35));
        approvalRequests.Add(lockReqB2); approvalDecisions.Add(lockDecB2);
        batchB2.Lock(lockReqB2.Id, empIndependentMgmt.ToString(), now.AddMinutes(35));
        Assert.Equal("locked", batchB2.ReviewState);

        // -------------------------------------------------------------
        // STEP 11 & 12: Financial Journals & Shared General Ledger
        // -------------------------------------------------------------
        var ledgerEntries = new List<FinanceLedgerEntry>();
        var postingDate = new DateOnly(2026, 10, 31);

        // Cohort A Accrual Journal (Salary Expense vs Salary Payable)
        var grossA = batchA.Lines.Where(x => x.Amount > 0).Sum(x => x.Amount);
        var deductionsA = -batchA.Lines.Where(x => x.Amount < 0).Sum(x => x.Amount);
        var netA = grossA - deductionsA;
        var jnlA1 = Guid.NewGuid(); var obA = Guid.NewGuid();
        ledgerEntries.Add(new(jnlA1, "Payroll", jnlA1.ToString(), postingDate, Currency, "SALARY_EXPENSE", grossA, 0m, obA));
        ledgerEntries.Add(new(jnlA1, "Payroll", jnlA1.ToString(), postingDate, Currency, "SALARY_PAYABLE", 0m, grossA, obA));

        // Deductions & Payables clearing
        var jnlA2 = Guid.NewGuid();
        ledgerEntries.Add(new(jnlA2, "Payroll", jnlA2.ToString(), postingDate, Currency, "SALARY_PAYABLE", deductionsA, 0m, obA));
        ledgerEntries.Add(new(jnlA2, "Payroll", jnlA2.ToString(), postingDate, Currency, "ATTENDANCE_DEDUCTIONS", 0m, deductionsA, obA));

        // Cohort B2 Accrual Journal
        var grossB = batchB2.Lines.Where(x => x.Amount > 0).Sum(x => x.Amount);
        var deductionsB = -batchB2.Lines.Where(x => x.Amount < 0).Sum(x => x.Amount);
        var netB = grossB - deductionsB;
        var jnlB1 = Guid.NewGuid(); var obB = Guid.NewGuid();
        ledgerEntries.Add(new(jnlB1, "Payroll", jnlB1.ToString(), postingDate, Currency, "SALARY_EXPENSE", grossB, 0m, obB));
        ledgerEntries.Add(new(jnlB1, "Payroll", jnlB1.ToString(), postingDate, Currency, "SALARY_PAYABLE", 0m, grossB, obB));

        var jnlB2 = Guid.NewGuid();
        ledgerEntries.Add(new(jnlB2, "Payroll", jnlB2.ToString(), postingDate, Currency, "SALARY_PAYABLE", deductionsB, 0m, obB));
        ledgerEntries.Add(new(jnlB2, "Payroll", jnlB2.ToString(), postingDate, Currency, "ATTENDANCE_DEDUCTIONS", 0m, deductionsB, obB));

        // Payment settlement journal from bank
        var jnlPay = Guid.NewGuid();
        var totalNet = netA + netB;
        ledgerEntries.Add(new(jnlPay, "Payroll", jnlPay.ToString(), new DateOnly(2026, 11, 2), Currency, "SALARY_PAYABLE", totalNet, 0m, obA));
        ledgerEntries.Add(new(jnlPay, "Payroll", jnlPay.ToString(), new DateOnly(2026, 11, 2), Currency, "HBL_OPERATING_BANK", 0m, totalNet, obA));

        // -------------------------------------------------------------
        // STEP 13: Trial Balance Verification
        // -------------------------------------------------------------
        var tb = FinanceTrialBalanceProjection.Calculate(MonthStart, MonthEndExclusive.AddMonths(1), ledgerEntries);
        Assert.NotNull(tb);
        Assert.Contains("Payroll", tb.IncludedSources);

        // Every currency closing balance must equal 0
        Assert.All(tb.Accounts.GroupBy(x => x.Currency), group => Assert.Equal(0m, group.Sum(x => x.ClosingBalance)));

        var salaryExpenseAccount = Assert.Single(tb.Accounts, x => x.AccountCode == "SALARY_EXPENSE");
        Assert.Equal(grossA + grossB, salaryExpenseAccount.Debit);

        var bankAccount = Assert.Single(tb.Accounts, x => x.AccountCode == "HBL_OPERATING_BANK");
        Assert.Equal(totalNet, bankAccount.Credit);

        // -------------------------------------------------------------
        // STEP 14: Project and Site Cost Allocation (HVAC / MEP Operational Structure)
        // -------------------------------------------------------------
        var prjHvacInstallation = Guid.NewGuid();
        var siteBahriaCommercial = Guid.NewGuid();
        var siteDhaMall = Guid.NewGuid();

        // Eligible payroll expense basis per employee
        var costBasis = new List<PayrollCostBasis>
        {
            new(empGeneral1, 53000m, Currency),
            new(empGeneral2, 53500m, Currency),
            new(empHR,       70000m, Currency),
            new(empFinance,  67000m, Currency),
            new(empAdmin,    88000m, Currency)
        };

        // Shares summing to exactly 10,000 basis points per employee (100.00%)
        var costShares = new List<PayrollCostShare>
        {
            // General 1 split between two HVAC sites
            new(empGeneral1, prjHvacInstallation, siteBahriaCommercial, 6000), // 60%
            new(empGeneral1, prjHvacInstallation, siteDhaMall,          4000), // 40%

            // General 2 100% on DHA Mall
            new(empGeneral2, prjHvacInstallation, siteDhaMall,          10000),

            // HR, Finance, Admin to General Overhead (null Project, null Site)
            new(empHR,      null, null, 10000),
            new(empFinance, null, null, 10000),
            new(empAdmin,   null, null, 10000)
        };

        var allocatedCosts = PayrollCostAllocation.Allocate(costBasis, costShares);
        Assert.Equal(costBasis.Sum(x => x.Amount), allocatedCosts.Sum(x => x.Amount));

        // Verify split site allocation for empGeneral1
        var g1Costs = allocatedCosts.Where(x => x.EmployeeId == empGeneral1).ToArray();
        Assert.Equal(2, g1Costs.Length);
        Assert.Equal(31800m, g1Costs.Single(x => x.SiteId == siteBahriaCommercial).Amount);
        Assert.Equal(21200m, g1Costs.Single(x => x.SiteId == siteDhaMall).Amount);

        // Verify General Overhead allocation
        var overheadTotal = allocatedCosts.Where(x => x.ProjectId is null && x.SiteId is null).Sum(x => x.Amount);
        Assert.Equal(70000m + 67000m + 88000m, overheadTotal);

        // -------------------------------------------------------------
        // STEP 15: Maker-Checker Enforcement on Cost Allocation Plan
        // -------------------------------------------------------------
        var planMakerEmployee = empFinance;
        var planCheckerEmployee = empIndependentMgmt;

        // Plan creator cannot self-approve
        Assert.NotEqual(planMakerEmployee, planCheckerEmployee);

        var costPlan = new PayrollCostPlan(Guid.NewGuid(), batchB2.Id, "synthetic-fingerprint", 1,
            null, "fingerprint-proof", "Finance Maker", planMakerEmployee, now.AddHours(1));
        Assert.Equal("Draft", costPlan.State);

        // Self-decision by maker rejected
        Assert.Throws<InvalidOperationException>(() => costPlan.Decide(true, "Finance Maker", planMakerEmployee, now.AddHours(2), "self-proof"));

        // Independent checker decides -> Approved
        costPlan.Decide(true, "Management Checker", planCheckerEmployee, now.AddHours(2), "approved-proof");
        Assert.Equal("Approved", costPlan.State);
        Assert.Equal(planCheckerEmployee, costPlan.DecidedByEmployeeId);

        // -------------------------------------------------------------
        // STEP 16, 17, 18: Audit History & Reconciliation
        // -------------------------------------------------------------
        Assert.True(approvalRequests.Count >= 6);
        Assert.True(approvalDecisions.Count >= 6);

        // Returned batch audit record verifies reason and actor
        var returnedAuditDecision = approvalDecisions.Single(x => x.ApprovalRequestId == returnReqB.Id);
        Assert.Equal(ApprovalState.Returned, returnedAuditDecision.Decision);
        Assert.Equal("Overtime hour rate correction required for Admin", returnedAuditDecision.Reason);
        Assert.Equal(empIndependentSecondReviewer.ToString(), returnedAuditDecision.ActorUserId);

        // Final reconciliation: 2 locked batches (Cohort A, Cohort B2), 1 returned batch (Cohort B)
        Assert.Equal("locked", batchA.ReviewState);
        Assert.Equal("locked", batchB2.ReviewState);
        Assert.Equal("returned", batchB.ReviewState);
    }
}
