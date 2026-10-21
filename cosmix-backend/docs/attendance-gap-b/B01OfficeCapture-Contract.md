# Office capture and boundary contract

Status: boundary/source/raw/session/capture source implemented and standalone
checks executed. Calculation/correction consumers, later schema application and
authenticated SQL/device acceptance are tracked separately, not inferred from routes.

## Company boundary

`GET /api/v1/company/offices/{branchId}/attendance-boundaries?asOfDate=2026-10-14`
returns exact effective history, branch rowversion and latest boundary version.
Missing configuration is explicit. The branch is a real organizational office,
not a Site created for attendance.

`POST` at the same path publishes a future version. Current SuperAdmin,
`org.company.manage` All scope, web MFA/CSRF and `Idempotency-Key` are required.
Supply the read branch version, expected latest revision, future effective date,
approved coordinates/radius/maximum accuracy, management attestation reference,
explicit jurisdiction and reason. No radius, location or jurisdiction defaults
exist. Stale/reused/retrospective/locked-range/conflicting history is refused.
The `201 Location` selects the published version's immutable effective date.
Approval is location configuration only, not legal payroll or biometric activation.

## Mobile duty

The existing own Check-In/Check-Out request does not accept a branch, roster,
membership, shift or boundary proof from the client. The server's employee roster
dispatcher and office source reader resolve those exact historical identities.
Office leave continues using its existing separate retained/revalidated source.

Check-In requires configured reliable in-radius office location, eligible current
membership, correct shift window, approved capture-validation policy, and an exact
unexpired single-use server face proof whose liveness AND identity passed. The
actual provider remains unavailable until deployment/model/device approvals exist.
Synthetic preverified proof in checks is not face/device acceptance.

Check-Out has no face attempt/proof. It requires reliable in-radius location at
the opening office boundary, current receipt membership and unchanged historical
opening roster/shift/member/calendar. An overnight duty retains its opening
business date. A transfer or changed source leaves raw evidence/exception for
review and does not create a false closure.

Raw evidence is append-only. Accepted office sessions require matching branch,
office roster, membership, boundary, calendar, raw business day and shift. A
closure appends a new session revision. Office and Site provenance cannot mix.
Office OT remains ineligible; these changes cannot enable financial penalties.

## Retry and response

Same-key identical request returns the protected receipt under current authority.
Changed payload conflicts; an expired receipt cannot authorize a second punch
under the permanent raw key. Read own punch history to reconcile it. Historical
receipt JSON is preserved by optional null-omitted office/validation properties.

New response fields `branchId`, `officeRosterAssignmentId`, `officeGeofenceVersionId`
identify office provenance. `validation` contains the exact final evidence ID,
separate capture-policy ID/revision/fingerprint, safe codes and own related raw
IDs. It does not expose biometric templates, device observation codes or risk
thresholds. Work-session responses also retain office membership/calendar IDs.
Candidate validation is not final success: async work may expire proof before the
last consumption gate, which records Rejected rather than Accepted final evidence.

## Verification limits

Standalone source/domain/authorization/replay/EF/OpenAPI checks are in the Gap B
ledger. No SQL schema application, real login/persistence, real face/device,
client UI or production acceptance is established by this document.
