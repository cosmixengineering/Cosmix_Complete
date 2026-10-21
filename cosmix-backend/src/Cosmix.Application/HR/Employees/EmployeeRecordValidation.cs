using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.Employees;

public static class EmployeeRecordValidation
{
    public static string Policy(EmployeeRecordPolicyOptions options, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(options.ApprovedVersion) || options.ApprovedVersion.Length > 100 ||
            options.ApprovedVersion != options.ApprovedVersion.Trim() || options.ApprovedVersion.Any(char.IsControl) ||
            options.ApprovedReviewMode != "FixedAdminReview" || !options.EffectiveFrom.HasValue ||
            options.EffectiveFrom == DateOnly.MinValue || options.EffectiveTo <= options.EffectiveFrom ||
            date < options.EffectiveFrom || options.EffectiveTo <= date)
            throw new Authentication.AuthenticationFailure("employee_record_policy_not_configured", 503);
        return options.ApprovedVersion;
    }
    public static string CreditTier(EmployeeFieldPolicyOptions options, string? value, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(options.ApprovedVersion) || options.ApprovedVersion.Length > 100 ||
            options.ApprovedVersion != options.ApprovedVersion.Trim() || options.ApprovedVersion.Any(char.IsControl) || !options.EffectiveFrom.HasValue ||
            options.EffectiveFrom == DateOnly.MinValue || options.EffectiveTo <= options.EffectiveFrom ||
            date < options.EffectiveFrom || options.EffectiveTo <= date || options.SimCreditTierValues is null ||
            options.SimCreditTierValues.Length == 0 || options.SimCreditTierValues.Any(x => string.IsNullOrWhiteSpace(x) ||
                x != x.Trim() || x.Length > 40 || x.Any(char.IsControl)) ||
            options.SimCreditTierValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.SimCreditTierValues.Length)
            throw new Authentication.AuthenticationFailure("employee_sim_credit_policy_not_configured", 503);
        var tier = options.SimCreditTierValues.SingleOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return tier ?? throw EmployeeOperation.Invalid("invalid_employee_sim_credit_tier");
    }
    public static string Text(string? text, int max, string code)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > max || text.Any(char.IsControl)) throw EmployeeOperation.Invalid(code);
        return text.Trim();
    }
    public static void Query(EmployeeRecordQuery query, bool states = false)
    {
        if (query is null || query.Page is < 1 or > 1000000 || query.PageSize is < 1 or > 100 ||
            !states && query.State.HasValue || query.State.HasValue && !Enum.IsDefined(query.State.Value))
            throw EmployeeOperation.Invalid("invalid_employee_record_query");
    }
    public static void Decision(DecideEmployeeRecord input)
    {
        if (input is null || input.Decision is not (ApprovalState.Approved or ApprovalState.Returned or ApprovalState.Rejected))
            throw EmployeeOperation.Invalid("invalid_employee_record_decision");
        Text(input.Reason, 1000, "employee_record_reason_required");
    }
    public static void Shape(EmployeeRecordKind kind, RequestEmployeeRecord input)
    {
        if (input is null || !Enum.IsDefined(input.Action) || input.EffectiveAt == default || input.EffectiveAt.Offset != TimeSpan.Zero ||
            input.ExpectedCurrentRecordId == Guid.Empty || (input.Action == EmployeeRecordAction.Issue) != !input.ExpectedCurrentRecordId.HasValue)
            throw EmployeeOperation.Invalid("invalid_employee_record_request");
        Text(input.Reason, 1000, "employee_record_reason_required");
        var transfer = input.Action == EmployeeRecordAction.Transfer;
        var issue = input.Action == EmployeeRecordAction.Issue;
        if (kind == EmployeeRecordKind.Asset)
        {
            if (input.Asset is not { } a || input.Sim is not null || input.Certification is not null ||
                input.Action is not (EmployeeRecordAction.Issue or EmployeeRecordAction.Return or EmployeeRecordAction.Transfer) ||
                a.EvidenceDocumentId == Guid.Empty || !issue != a.ReceiverAccountId.HasValue || a.ReceiverAccountId == Guid.Empty ||
                transfer != a.TargetEmployeeId.HasValue || a.TargetEmployeeId == Guid.Empty || transfer != a.TargetEvidenceDocumentId.HasValue ||
                a.TargetEvidenceDocumentId == Guid.Empty || transfer != !string.IsNullOrWhiteSpace(a.ExpectedTargetVersion) ||
                transfer != !string.IsNullOrWhiteSpace(a.TargetCondition) || issue != !string.IsNullOrWhiteSpace(a.AssetReference))
                throw EmployeeOperation.Invalid("invalid_asset_record_request");
            Text(a.Condition, 1000, "asset_condition_required");
            if (issue) Text(a.AssetReference, 100, "asset_reference_required");
            if (transfer) Text(a.TargetCondition, 1000, "asset_condition_required");
        }
        else if (kind == EmployeeRecordKind.Sim)
        {
            if (input.Sim is not { } s || input.Asset is not null || input.Certification is not null ||
                input.Action is not (EmployeeRecordAction.Issue or EmployeeRecordAction.Return or EmployeeRecordAction.Transfer) ||
                s.EvidenceDocumentId == Guid.Empty || !issue != s.ReceiverAccountId.HasValue || s.ReceiverAccountId == Guid.Empty ||
                transfer != s.TargetEmployeeId.HasValue || s.TargetEmployeeId == Guid.Empty || transfer != s.TargetEvidenceDocumentId.HasValue ||
                s.TargetEvidenceDocumentId == Guid.Empty || transfer != !string.IsNullOrWhiteSpace(s.ExpectedTargetVersion) ||
                issue != !string.IsNullOrWhiteSpace(s.SimReference) || issue != !string.IsNullOrWhiteSpace(s.PhoneNumber) ||
                (issue || transfer) != !string.IsNullOrWhiteSpace(s.CreditTierCode)) throw EmployeeOperation.Invalid("invalid_sim_record_request");
            if (issue) Text(s.SimReference, 100, "sim_reference_required");
        }
        else if (kind == EmployeeRecordKind.Certification)
        {
            if (input.Certification is not { } c || input.Asset is not null || input.Sim is not null ||
                input.Action is not (EmployeeRecordAction.Issue or EmployeeRecordAction.Renew or EmployeeRecordAction.Replace or EmployeeRecordAction.Suspend or EmployeeRecordAction.Revoke) ||
                c.EvidenceDocumentId == Guid.Empty) throw EmployeeOperation.Invalid("invalid_certification_record_request");
            var metadata = input.Action is EmployeeRecordAction.Issue or EmployeeRecordAction.Renew or EmployeeRecordAction.Replace;
            if (metadata != !string.IsNullOrWhiteSpace(c.Type) || metadata != !string.IsNullOrWhiteSpace(c.Number) ||
                metadata != !string.IsNullOrWhiteSpace(c.Issuer) || metadata != c.IssuedOn.HasValue ||
                !metadata && c.ExpiresOn.HasValue) throw EmployeeOperation.Invalid("invalid_certification_metadata");
            if (metadata)
            {
                Text(c.Type, 100, "certification_type_required"); Text(c.Number, 100, "certification_number_required");
                Text(c.Issuer, 200, "certification_issuer_required");
                if (c.IssuedOn == DateOnly.MinValue || c.ExpiresOn < c.IssuedOn) throw EmployeeOperation.Invalid("invalid_certification_dates");
            }
        }
        else throw EmployeeOperation.Invalid("invalid_employee_record_kind");
    }
}
