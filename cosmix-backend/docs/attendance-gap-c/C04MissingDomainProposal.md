# Live Parent-Owned Domain Eligibility Hook

Parent owns the Domain types, AdvancedAttendanceRulesCodec validation and
publication surface. The sidecar must consume the typed property directly.
The existing constructor remains unchanged; no Application dependency belongs
in Domain and no separate policy family or JSON bag is needed.

```csharp
// Namespace: Cosmix.Domain.HR.Policies
// Add using Cosmix.Domain.Common.Approvals for the existing ApprovalState.
public enum KpiMissingCaseSetting { MedicalSickLeave = 1, EmergencyHalfDay = 2 }
public enum KpiMissingAssessmentSetting { AtIndependentReview = 1 }
public enum KpiMissingDurationSetting { DatedRosterWindow = 1, PublishedLeaveStandardDay = 2 }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record KpiMissingEligibilityRules(
    [property: JsonRequired] IReadOnlyList<KpiMissingCaseSetting>? Cases,
    [property: JsonRequired] KpiMissingAssessmentSetting? AssessmentBasis,
    [property: JsonRequired] KpiMissingDurationSetting? DurationBasis,
    [property: JsonRequired] IReadOnlyList<ApprovalState>? AllowedLeaveStates);

// Optional init member on the existing KpiRules record; no constructor change:
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public KpiMissingEligibilityRules? MissingEvidenceEligibility { get; init; }
```

The live parent AdvancedAttendanceRulesCodec requires this object when
MissingEvidenceEffectEnabled is true. Require nonempty distinct known Cases
(maximum two), AssessmentBasis exactly AtIndependentReview, an explicitly known
DurationBasis, and nonempty distinct AllowedLeaveStates (maximum four) limited
to Pending, Approved, Returned or Rejected. All null/unknown choices produce
explicit field errors. Do not default a missing choice or instantiate settings
automatically. MissingEvidencePoints remains the explicitly published rate.

The existing camelCase publication/parse contract will serialize and recognize
kpi.missingEvidenceEligibility as a known typed init member. Disabled effects
do not require the object. Existing no-default authority, schema and publication
guards remain in force. Disabled effects require the eligibility object to be
null, with no inferred rate or setting. A future deadline/grace interpretation needs its own
approved typed extension and verified source; no deadline field is fabricated.

These parent-owned types/property are live. Application has removed its
duplicate eligibility enums/settings and uses direct
rules.Kpi.MissingEvidenceEligibility access. The sidecar's
own validation is a defensive read guard; it does not substitute for codec
publication validation. No Domain source has been edited by this sidecar owner.

Actual evidence remains LeaveEvidence.ReadAsync plus the protected submitted
LeaveRequest and exact LeaveRequestDay, original approval/decision/cancellation
provenance, saved factual day and dated roster. The setting selects when and
which already-typed case is eligible; it cannot assert missing evidence or change
the leave request's document state, pay treatment or approval.
