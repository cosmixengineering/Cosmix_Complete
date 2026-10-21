using System.Text.Json;
using Cosmix.Application.Attendance.Common;
using Cosmix.Application.Attendance.Rosters;
using Cosmix.Application.Authentication;
using Cosmix.Domain.Common.Approvals;

namespace Cosmix.Application.HR.VehicleFuel;

public sealed partial class VehicleFuelService
{
    public const string IncidentAmendmentRequestType = "vehicle.incident.amendment";
    private static string AmendmentPurpose(Guid incidentId) => $"vehicle:incident:amendment:{incidentId:N}";

    public Task<VehicleIncidentAmendmentView> RequestIncidentAmendmentAsync(Guid incidentId, ProposeVehicleIncidentAmendmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => request is null ? throw AttendanceOperation.Invalid("vehicle_adjustment_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Incident, "vehicle.incident.amendment.request", null, key, new { incidentId, request }, request.Reason,
            context, async (actor, token) =>
            {
                ValidateIncidentAmendment(request);
                var history = await IncidentHistory(incidentId, token);
                if (history.Current.DecisionId != request.ExpectedDecisionId) throw AttendanceOperation.Conflict("vehicle_incident_predecessor_changed");
                if (history.Amendments.Any(x => x.State == ApprovalState.Pending.ToString())) throw AttendanceOperation.Conflict("vehicle_incident_amendment_pending");
                var incident = await repository.FindIncidentAsync(incidentId, token) ?? throw AttendanceOperation.Missing("incident_not_found");
                var custody = await repository.FindCustodyAsync(incident.VehicleCustodyId, token) ?? throw AttendanceOperation.Conflict("vehicle_incident_custody_unavailable");
                var (version, _) = await LoadPolicyAsync(RosterSchedule.Today(actor.Now), token);
                var rule = AdjustmentPolicy(version)?.IncidentAmendments ?? throw AttendanceOperation.Conflict("vehicle_incident_amendment_not_configured");
                if (!rule.AllowedPriorOutcomes.Contains(history.Current.Outcome)) throw AttendanceOperation.Conflict("vehicle_incident_outcome_amendment_not_allowed");
                if (history.Amendments.Count >= 1000) throw AttendanceOperation.Conflict("vehicle_adjustment_history_limit");
                var revision = history.Amendments.Count + 1;
                var snapshot = new VehicleIncidentAmendmentSnapshot(1, revision, incidentId, incident.VehicleCustodyId, custody.EmployeeId,
                    request.ExpectedDecisionId, version.Id, rule, request, actor.Identity.AccountId, actor.Now);
                var reference = AttendanceOperation.Reference(await files.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(snapshot), AmendmentPurpose(incidentId), token));
                await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Incident, actor, token);
                var approval = new ApprovalRequest(IncidentAmendmentRequestType, incidentId, actor.Identity.AccountId.ToString(), reference, revision, actor.Now);
                repository.AddVehicleAdjustmentRequest(approval); await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleIncidentAmendmentView>(approval.Id, null, AmendmentView(approval, snapshot, null));
            }, ct, beforeReplay: (actor, token) => RequireCurrentVehicleAuthority(VehicleFuelPermissions.Incident, actor, token),
            beforeCommit: (actor, token) => RequireCurrentVehicleAuthority(VehicleFuelPermissions.Incident, actor, token));

    public Task<VehicleIncidentAmendmentView> ReviewIncidentAmendmentAsync(Guid incidentId, Guid requestId, ReviewVehicleAdjustmentRequest request,
        string key, AuthRequestContext context, CancellationToken ct) => request is null ? throw AttendanceOperation.Invalid("vehicle_adjustment_request_required") :
        operation.WriteAsync(VehicleFuelPermissions.Incident, "vehicle.incident.amendment.review", null, key, new { incidentId, requestId, request }, request.Reason,
            context, async (actor, token) =>
            {
                var approval = await AdjustmentRequest(incidentId, requestId, IncidentAmendmentRequestType, token);
                var snapshot = await AmendmentSnapshot(approval, token);
                ExpectAdjustment(approval, request);
                var history = await IncidentHistory(incidentId, token);
                if (history.Current.DecisionId != snapshot.PreviousDecisionId || history.Amendments.LastOrDefault()?.RequestId != requestId)
                    throw AttendanceOperation.Conflict("vehicle_incident_predecessor_changed");
                var before = AmendmentView(approval, snapshot, null);
                var decision = await DecideAdjustment(approval, request, snapshot, VehicleFuelPermissions.Incident, actor, token);
                await repository.SaveChangesAsync(token);
                return new AttendanceChange<VehicleIncidentAmendmentView>(approval.Id, before, AmendmentView(approval, snapshot, decision));
            }, ct, beforeReplay: (actor, token) => RequireIncidentAmendmentAuthority(incidentId, requestId, actor, token),
            beforeCommit: (actor, token) => RequireIncidentAmendmentAuthority(incidentId, requestId, actor, token));

    private async Task RequireIncidentAmendmentAuthority(Guid incidentId, Guid requestId, AttendanceActor actor, CancellationToken ct)
    {
        var approval = await AdjustmentRequest(incidentId, requestId, IncidentAmendmentRequestType, ct);
        var snapshot = await AmendmentSnapshot(approval, ct);
        RequireIndependentReviewer(snapshot.EmployeeId, approval.RequesterUserId, actor);
        RequireAdjustmentRole(snapshot.Rule.ReviewerRoleCodes, actor);
        var (_, policy) = await LoadPolicyAsync(RosterSchedule.Today(snapshot.RequestedAt), ct, snapshot.PolicyVersionId);
        RequireWorkflow(policy).RequireIncidentReviewer(actor.RoleCode);
        await RequireCurrentVehicleAuthority(VehicleFuelPermissions.Incident, actor, ct);
    }

    public Task<VehicleIncidentReviewHistory> IncidentReviewHistoryAsync(Guid incidentId, AuthRequestContext context, CancellationToken ct) =>
        operation.ReadAsync(VehicleFuelPermissions.Incident, "vehicle.incident.review.history", null, context,
            (_, token) => IncidentHistory(incidentId, token), ct);

    private async Task<VehicleIncidentReviewHistory> IncidentHistory(Guid incidentId, CancellationToken ct)
    {
        var incident = await repository.FindIncidentAsync(incidentId, ct) ?? throw AttendanceOperation.Missing("incident_not_found");
        var original = await OriginalIncidentReview(incident, ct); var current = original;
        var headers = await repository.ListVehicleAdjustmentRequestsAsync(incidentId, IncidentAmendmentRequestType, ct);
        ValidateAdjustmentHeaders(headers, incidentId, IncidentAmendmentRequestType);
        var views = new List<VehicleIncidentAmendmentView>();
        foreach (var header in headers.OrderBy(x => x.Stage))
        {
            var snapshot = await AmendmentSnapshot(header, ct);
            if (snapshot.CustodyId != incident.VehicleCustodyId || snapshot.PreviousDecisionId != current.DecisionId)
                throw AttendanceOperation.Conflict("vehicle_incident_amendment_history_invalid");
            var decision = await AdjustmentDecision(header, snapshot, ct);
            views.Add(AmendmentView(header, snapshot, decision));
            if (header.State == ApprovalState.Approved)
            {
                var proposal = snapshot.Proposal;
                current = new(incidentId, header.Id, decision!.Id, proposal.Outcome, IncidentDecisionState(proposal.Outcome).ToString(),
                    proposal.ProposedEmployeeLiability, proposal.Currency, proposal.InsuranceClaimReference, proposal.EvidenceReference, proposal.Reason,
                    snapshot.PolicyVersionId, decision.DecidedAt, false, false);
            }
        }
        return new(original, current, views);
    }

    private async Task<VehicleIncidentAmendmentSnapshot> AmendmentSnapshot(ApprovalRequest header, CancellationToken ct)
    {
        var snapshot = await operation.ReadPrivateAsync<VehicleIncidentAmendmentSnapshot>(header.SnapshotPrivateReference, AmendmentPurpose(header.SubjectId), ct);
        if (snapshot.SchemaVersion != 1 || snapshot.Revision != header.Stage || snapshot.IncidentId != header.SubjectId || snapshot.EmployeeId == Guid.Empty ||
            snapshot.RequestedByAccountId.ToString() != header.RequesterUserId || snapshot.RequestedAt != header.RequestedAt || snapshot.Rule is null ||
            snapshot.Proposal is null || snapshot.Proposal.ExpectedDecisionId != snapshot.PreviousDecisionId)
            throw AttendanceOperation.Conflict("vehicle_incident_amendment_evidence_invalid");
        return snapshot;
    }

    private static void ValidateIncidentAmendment(ProposeVehicleIncidentAmendmentRequest request)
    {
        AdjustmentEvidence(request.EvidenceReference);
        if (request.ExpectedDecisionId == Guid.Empty || !Enum.IsDefined(request.Outcome)) throw AttendanceOperation.Invalid("vehicle_incident_amendment_invalid");
        if (request.Outcome == VehicleIncidentReviewOutcome.ProposedLiabilityRecorded)
        {
            if (request.ProposedEmployeeLiability is null or <= 0 or > 10_000_000 || decimal.Round(request.ProposedEmployeeLiability.Value, 2) != request.ProposedEmployeeLiability ||
                request.Currency is not { Length: 3 } || request.Currency.Any(c => c is < 'A' or > 'Z')) throw AttendanceOperation.Invalid("incident_liability_currency_required");
        }
        else if (request.ProposedEmployeeLiability is not null || request.Currency is not null) throw AttendanceOperation.Invalid("vehicle_incident_outcome_amount_not_allowed");
        if (request.InsuranceClaimReference is { } reference && (!VehicleAdjustmentPolicy.Evidence(reference) || reference.Length > 200) ||
            request.Outcome == VehicleIncidentReviewOutcome.InsuranceReviewRequired && request.InsuranceClaimReference is null)
            throw AttendanceOperation.Invalid("vehicle_incident_insurance_reference_required");
    }
    private static VehicleIncidentAmendmentView AmendmentView(ApprovalRequest row, VehicleIncidentAmendmentSnapshot s, ApprovalDecision? d) =>
        new(row.Id, row.Stage, Convert.ToBase64String(row.RowVersion), row.State.ToString(), s.IncidentId, s.PreviousDecisionId,
            s.PolicyVersionId, s.Proposal, s.RequestedByAccountId, s.RequestedAt, d?.Id, d?.Reason);
    private sealed record VehicleIncidentAmendmentSnapshot(int SchemaVersion, int Revision, Guid IncidentId, Guid CustodyId, Guid EmployeeId,
        Guid PreviousDecisionId, Guid PolicyVersionId, VehicleIncidentAmendmentRule Rule, ProposeVehicleIncidentAmendmentRequest Proposal,
        Guid RequestedByAccountId, DateTimeOffset RequestedAt);
}
