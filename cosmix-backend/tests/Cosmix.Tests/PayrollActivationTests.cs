using System.Reflection;
using Cosmix.Application.Attendance.PayrollInputs;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;
using Xunit;

namespace Cosmix.Tests;

public class PayrollActivationTests
{
    [Fact]
    public void Exact_independent_approved_operational_evidence_produces_retained_fingerprint()
    {
        var f = new Fixture();
        var fingerprint = f.Validate();
        Assert.Matches("^[A-F0-9]{64}$", fingerprint);
        Assert.Equal(fingerprint, f.Validate());
    }

    [Theory]
    [InlineData("maker")]
    [InlineData("beneficiary")]
    [InlineData("different-request")]
    [InlineData("different-source")]
    [InlineData("different-policy")]
    [InlineData("different-jurisdiction")]
    [InlineData("different-coverage")]
    [InlineData("missing-decision")]
    [InlineData("duplicate-decision")]
    [InlineData("pending")]
    [InlineData("rejected")]
    [InlineData("different-type")]
    [InlineData("different-stage")]
    [InlineData("different-request-sha")]
    [InlineData("different-decision-time")]
    [InlineData("different-reviewer-role")]
    [InlineData("invalid-document-sha")]
    [InlineData("blank-statement")]
    [InlineData("unknown-jurisdiction")]
    public void Missing_changed_or_unauthorized_evidence_cannot_activate(string mutation)
    {
        var f = new Fixture();
        switch (mutation)
        {
            case "maker": f.Decision = f.Decision with { ReviewerAccountId = f.Request.RequesterAccountId }; break;
            case "beneficiary": f.Decision = f.Decision with { ReviewerEmployeeId = f.Binding.EmployeeId }; break;
            case "different-request": f.Decision = f.Decision with { RequestId = Guid.NewGuid() }; break;
            case "different-source": f.Binding = f.Binding with { SourceRevisionId = Guid.NewGuid() }; break;
            case "different-policy": f.Binding = f.Binding with { PolicyVersionIds = [Guid.NewGuid()] }; break;
            case "different-jurisdiction": f.Binding = f.Binding with { Jurisdictions = [f.Binding.Jurisdictions[0] with { JurisdictionCode = "OTHER" }] }; break;
            case "different-coverage": f.Binding = f.Binding with { CoverageFingerprint = new('B', 64) }; break;
            case "missing-decision": f.Row = f.Row with { Decisions = [] }; break;
            case "duplicate-decision": f.Row = f.Row with { Decisions = [f.Row.Decisions[0], f.Row.Decisions[0]] }; break;
            case "pending": Set(f.Row.Request, "State", ApprovalState.Pending); break;
            case "rejected": Set(f.Row.Request, "State", ApprovalState.Rejected); break;
            case "different-type": Set(f.Row.Request, "RequestType", "other.request"); break;
            case "different-stage": Set(f.Row.Request, "Stage", 2); break;
            case "different-request-sha": f.RequestSha = new('B', 64); break;
            case "different-decision-time": f.Decision = f.Decision with { DecidedAtUtc = f.Decision.DecidedAtUtc.AddMinutes(1) }; break;
            case "different-reviewer-role": f.Decision = f.Decision with { ReviewerRoleCode = "OTHER" }; break;
            case "invalid-document-sha": f.Request = f.Request with { EvidenceSha256 = "not-a-hash" }; break;
            case "blank-statement": f.Request = f.Request with { Statement = "" }; break;
            case "unknown-jurisdiction": f.Binding = f.Binding with { Jurisdictions = [f.Binding.Jurisdictions[0] with { State = "unavailable" }] }; break;
        }
        var error = Assert.Throws<AuthenticationFailure>(() => f.Validate());
        Assert.Equal(409, error.StatusCode);
    }

    private static void Set(object value, string property, object changed) => value.GetType().GetProperty(property)!.SetValue(value, changed);
    private sealed class Fixture
    {
        public AttendancePayrollActivationBinding Binding;
        public AttendancePayrollActivationRequestEvidence Request;
        public AttendancePayrollActivationDecisionEvidence Decision;
        public AttendancePayrollActivationRow Row;
        public string RequestSha;
        public Fixture()
        {
            var now = new DateTimeOffset(2027, 2, 1, 10, 0, 0, TimeSpan.Zero);
            var maker = Guid.NewGuid(); var reviewer = Guid.NewGuid(); var source = Guid.NewGuid();
            Binding = new(Guid.NewGuid(), new(2027, 1, 1), source, 1, new('A', 64), new('C', 64), "test-calculation-v1",
                new(2027, 1, 1), new(2027, 1, 31), new('D', 64), [Guid.NewGuid()],
                [new(new(2027, 1, 1), Guid.NewGuid(), Guid.NewGuid(), "PK-SD", "configured")]);
            Request = new(1, Binding, maker, now, "TEST ONLY review document", new('E', 64),
                "TEST ONLY company operational approval; no legal certification", "Synthetic responsibility review");
            RequestSha = AttendancePayrollActivationProof.Hash(Request);
            var row = new ApprovalRequest("attendance.payroll-activation", source, maker.ToString(), "private-request", 1, now);
            var approved = row.Decide(ApprovalState.Approved, reviewer.ToString(), "HR", "Independent synthetic review", now.AddMinutes(1), "private-decision");
            Row = new(row, [approved]);
            Decision = new(1, row.Id, RequestSha, AttendancePayrollActivationProof.Hash(Binding), 1,
                reviewer, Guid.NewGuid(), "HR", now.AddMinutes(1), "Independent synthetic review");
        }
        public string Validate() => AttendancePayrollActivationProof.RequireApproved(Row, Request, Decision, RequestSha, Binding);
    }
}
