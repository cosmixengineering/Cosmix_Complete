using Cosmix.Application.Attendance.LocationConsent;
using Cosmix.Application.Attendance.PunchRetention;
using Cosmix.Domain.Attendance.LocationConsent;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.StatutoryContributions;
using Xunit;

namespace Cosmix.Tests;

public class ComplianceTests
{
    [Fact]
    public void StatutoryContribution_Approve_PreventsCreatorFromApproving()
    {
        // Second-person approval enforcement test
        var creator = "user-hr-preparer";
        var approver = "user-hr-preparer"; // Same user
        var now = DateTimeOffset.UtcNow;

        var contribution = new StatutoryContribution(
            StatutoryScheme.EOBI,
            new DateOnly(2026, 10, 1),
            employeeContributionAmount: 500m,
            employerContributionAmount: 2500m,
            currency: "PKR",
            createdByUserId: creator,
            createdAt: now);

        Assert.Equal(ContributionState.Draft, contribution.State);

        // Should throw when creator attempts self-approval
        var ex = Assert.Throws<InvalidOperationException>(() => contribution.Approve(approver, now));
        Assert.Contains("creator cannot approve", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Separate user CAN approve successfully
        var authorizedApprover = "user-finance-manager";
        contribution.Approve(authorizedApprover, now);
        Assert.Equal(ContributionState.Approved, contribution.State);
        Assert.Equal(authorizedApprover, contribution.ApprovedByUserId);
    }

    [Fact]
    public void StatutoryContribution_Post_RequiresApprovedState()
    {
        var contribution = new StatutoryContribution(
            StatutoryScheme.PESSI,
            new DateOnly(2026, 10, 1),
            employeeContributionAmount: 0m,
            employerContributionAmount: 1800m,
            currency: "PKR",
            createdByUserId: "user-1",
            createdAt: DateTimeOffset.UtcNow);

        // Cannot post while still in Draft
        Assert.Throws<InvalidOperationException>(() => contribution.Post("user-2", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StatutoryContribution_RequiresAtLeastOnePositiveAmount()
    {
        Assert.Throws<ArgumentException>(() => new StatutoryContribution(
            StatutoryScheme.SESSI,
            new DateOnly(2026, 10, 1),
            employeeContributionAmount: 0m,
            employerContributionAmount: 0m,
            currency: "PKR",
            createdByUserId: "user-1",
            createdAt: DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PayrollObligation_OnlyDraftCanBeFinalized()
    {
        var obligation = new PayrollObligation(
            batchId: Guid.NewGuid(),
            totalGrossAmount: 150000m,
            totalNetAmount: 135000m,
            currency: "PKR",
            createdByUserId: "finance-user",
            createdAt: DateTimeOffset.UtcNow);

        Assert.Equal(PayrollObligationState.Draft, obligation.State);
        obligation.Finalize("finance-manager", DateTimeOffset.UtcNow);
        Assert.Equal(PayrollObligationState.Finalized, obligation.State);

        // Cannot finalize twice
        Assert.Throws<InvalidOperationException>(() => obligation.Finalize("finance-manager", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void SalaryPayment_RequiresAccrualJournal_AndEnforcesConfirmationState()
    {
        var obligationId = Guid.NewGuid();
        var accrualJournalId = Guid.NewGuid();
        var paymentJournalId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var payment = new SalaryPayment(
            obligationId,
            accrualJournalId,
            paymentReference: "FT-202610-001",
            amount: 135000m,
            createdByUserId: "finance-disburser",
            createdAt: now);

        Assert.Equal(SalaryPaymentState.Pending, payment.State);

        // Cannot confirm pending payment before journal is posted
        Assert.Throws<InvalidOperationException>(() => payment.Confirm("finance-approver", now));

        // Record payment journal transitions to Posted
        payment.RecordPaymentJournal(paymentJournalId);
        Assert.Equal(SalaryPaymentState.Posted, payment.State);
        Assert.Equal(paymentJournalId, payment.PaymentJournalEntryId);

        // Creator cannot confirm their own payment disbursement (second-person approval rule)
        var selfApprovalEx = Assert.Throws<InvalidOperationException>(() => payment.Confirm("finance-disburser", now));
        Assert.Contains("creator cannot confirm", selfApprovalEx.Message, StringComparison.OrdinalIgnoreCase);

        // Now separate Finance approver can confirm
        payment.Confirm("finance-approver", now);
        Assert.Equal(SalaryPaymentState.Confirmed, payment.State);
        Assert.Equal("finance-approver", payment.ConfirmedByUserId);

        // Double confirmation is strictly prohibited
        Assert.Throws<InvalidOperationException>(() => payment.Confirm("another-user", now));
    }

    [Fact]
    public void LocationNoticePolicy_Publishing_SetsActiveAndVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new LocationNoticePolicy(
            version: 1,
            noticeText: "Company notice regarding attendance location verification.",
            createdByUserId: "hr-admin",
            createdAt: now);

        Assert.False(policy.IsActive);
        policy.Publish("hr-admin", now);
        Assert.True(policy.IsActive);
        Assert.Equal(1, policy.Version);

        policy.Deactivate();
        Assert.False(policy.IsActive);
    }

    [Fact]
    public void StatutoryContribution_StoresEmployerRegistrationNumber()
    {
        var contribution = new StatutoryContribution(
            StatutoryScheme.EOBI,
            new DateOnly(2026, 10, 1),
            employeeContributionAmount: 500m,
            employerContributionAmount: 2500m,
            currency: "PKR",
            createdByUserId: "user-1",
            createdAt: DateTimeOffset.UtcNow,
            payableAccountCode: "2030-EOBI",
            notes: "October challan",
            employerRegistrationNumber: "BE-12345678");

        Assert.Equal("BE-12345678", contribution.EmployerRegistrationNumber);
    }

    [Fact]
    public void StatutoryContribution_PostRequiresPayableAccountCode()
    {
        var contribution = new StatutoryContribution(
            StatutoryScheme.PESSI,
            new DateOnly(2026, 10, 1),
            employeeContributionAmount: 0m,
            employerContributionAmount: 1800m,
            currency: "PKR",
            createdByUserId: "user-1",
            createdAt: DateTimeOffset.UtcNow,
            payableAccountCode: null); // missing account code

        contribution.Approve("user-2", DateTimeOffset.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            contribution.Post("user-finance", DateTimeOffset.UtcNow));
        Assert.Contains("Payable account code is required", ex.Message);
    }

    [Fact]
    public void AccessPermissionCatalog_ContainsCompliancePermissions()
    {
        var accrue = Cosmix.Application.Authentication.Access.AccessPermissionCatalog.Find("payroll.accounting.accrue");
        Assert.NotNull(accrue);

        var statutoryManage = Cosmix.Application.Authentication.Access.AccessPermissionCatalog.Find("statutory.contributions.manage");
        Assert.NotNull(statutoryManage);

        var consentAdmin = Cosmix.Application.Authentication.Access.AccessPermissionCatalog.Find("location.consent.admin");
        Assert.NotNull(consentAdmin);
    }

    [Fact]
    public async Task PunchRetention_FailsSafelyIfRetentionDaysNotConfigured()
    {
        var options = new PunchRetentionOptions
        {
            RetentionDays = null, // Not configured
        };

        var service = new PunchRetentionService(
            new FakePunchRetentionRepository(),
            options,
            TimeProvider.System);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunRetentionAsync(CancellationToken.None));
        Assert.NotNull(ex);
    }

    private sealed class FakePunchRetentionRepository : IPunchRetentionRepository
    {
        public Task<int> RedactBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset redactedAtUtc,
            int retentionDays, int batchSize, CancellationToken ct) => Task.FromResult(0);
        public Task<int> ExpireLegacyAuditBatchAsync(DateTimeOffset thresholdUtc, DateTimeOffset atUtc,
            int batchSize, CancellationToken ct) => Task.FromResult(0);
    }
}
