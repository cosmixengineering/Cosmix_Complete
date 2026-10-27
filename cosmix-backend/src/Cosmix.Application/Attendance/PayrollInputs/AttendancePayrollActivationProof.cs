using System.Security.Cryptography;
using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.Attendance.PayrollInputs;

internal static class AttendancePayrollActivationProof
{
    internal static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static string RequireApproved(AttendancePayrollActivationRow row,
        AttendancePayrollActivationRequestEvidence request, AttendancePayrollActivationDecisionEvidence decision,
        string requestEvidenceSha256, AttendancePayrollActivationBinding binding)
    {
        var fingerprint = RequireDecision(row, request, decision, requestEvidenceSha256, binding);
        if (row.Request.State != ApprovalState.Approved) throw Invalid();
        return fingerprint;
    }

    internal static void RequireRequest(AttendancePayrollActivationRow row,
        AttendancePayrollActivationRequestEvidence evidence, string evidenceSha256, AttendancePayrollActivationBinding binding)
    {
        RequireBinding(binding);
        var request = row.Request;
        if (evidence.SchemaVersion != 1 || Hash(evidence) != evidenceSha256 || Hash(evidence.Binding) != Hash(binding) ||
            request.RequestType != "attendance.payroll-activation" || request.Stage != 1 || request.RouteRuleId is not null ||
            request.SubjectId != binding.SourceRevisionId || !Guid.TryParse(request.RequesterUserId, out var maker) ||
            maker == Guid.Empty || maker != evidence.RequesterAccountId || request.RequestedAt != evidence.RequestedAtUtc ||
            !Text(evidence.EvidenceReference, 500) || !IsHash(evidence.EvidenceSha256) ||
            !Text(evidence.Statement, 4000) || evidence.Statement.Trim().Length < 20 || !Text(evidence.Reason, 1000) ||
            !Enum.IsDefined(request.State) || request.State == ApprovalState.Cancelled ||
            (request.State == ApprovalState.Pending ? request.ClosedAt is not null || row.Decisions.Count != 0 :
                request.ClosedAt is null || request.ClosedAt < request.RequestedAt || row.Decisions.Count != 1)) throw Invalid();
    }

    internal static string RequireDecision(AttendancePayrollActivationRow row,
        AttendancePayrollActivationRequestEvidence request, AttendancePayrollActivationDecisionEvidence evidence,
        string requestEvidenceSha256, AttendancePayrollActivationBinding binding)
    {
        RequireRequest(row, request, requestEvidenceSha256, binding);
        if (row.Decisions.Count != 1) throw Invalid();
        var decision = row.Decisions[0];
        if (evidence.SchemaVersion != 1 || evidence.RequestId != row.Request.Id ||
            evidence.RequestEvidenceSha256 != requestEvidenceSha256 || evidence.BindingFingerprint != Hash(binding) ||
            evidence.Decision is < 1 or > 3 || (ApprovalState)evidence.Decision != row.Request.State ||
            decision.ApprovalRequestId != row.Request.Id || decision.Decision != row.Request.State ||
            !Guid.TryParse(decision.ActorUserId, out var reviewer) || reviewer == Guid.Empty ||
            reviewer != evidence.ReviewerAccountId || reviewer == request.RequesterAccountId ||
            evidence.ReviewerEmployeeId == Guid.Empty || evidence.ReviewerEmployeeId == binding.EmployeeId ||
            decision.ActorRoleCode != evidence.ReviewerRoleCode || !Text(evidence.ReviewerRoleCode, 100) ||
            decision.DecidedAt != evidence.DecidedAtUtc || decision.DecidedAt != row.Request.ClosedAt ||
            decision.DecidedAt < request.RequestedAtUtc || decision.Reason != evidence.Reason ||
            !Text(evidence.Reason, 1000) || string.IsNullOrWhiteSpace(decision.ProtectedChangeReference)) throw Invalid();
        return Hash(new { row.Request.Id, DecisionId = decision.Id, requestEvidenceSha256,
            DecisionEvidenceSha256 = Hash(evidence), BindingFingerprint = Hash(binding) });
    }

    internal static void RequireBinding(AttendancePayrollActivationBinding binding)
    {
        if (binding.EmployeeId == Guid.Empty || binding.SourceRevisionId == Guid.Empty || binding.SourceRevision < 1 ||
            binding.MonthStart.Day != 1 || binding.SourceFrom > binding.SourceTo || binding.SourceTo < binding.MonthStart ||
            binding.SourceTo >= binding.MonthStart.AddMonths(1) || !IsHash(binding.SourceFingerprint) ||
            !IsHash(binding.SourceEvidenceSha256) || !IsHash(binding.CoverageFingerprint) ||
            !Text(binding.CalculationVersion, 100) || binding.PolicyVersionIds is not { Count: > 0 and <= 732 } ||
            binding.PolicyVersionIds.Any(x => x == Guid.Empty) || binding.PolicyVersionIds.Distinct().Count() != binding.PolicyVersionIds.Count ||
            binding.Jurisdictions is not { Count: > 0 and <= 732 } ||
            binding.Jurisdictions.Select(x => x.BusinessDate).Distinct().Count() != binding.Jurisdictions.Count ||
            binding.Jurisdictions.Any(x => x.BusinessDate < binding.SourceFrom || x.BusinessDate > binding.SourceTo ||
                x.State != "configured" || x.ConfigurationVersionId is null || x.ConfigurationVersionId == Guid.Empty ||
                !Text(x.JurisdictionCode, 100))) throw Invalid();
    }

    internal static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    internal static bool Text(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum &&
        !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));
    private static Cosmix.Application.Authentication.AuthenticationFailure Invalid() =>
        AttendanceOperation.Conflict("payroll_activation_evidence_invalid");
}
