# HR foundation policy publication

The field, lifecycle and operational record authority policy module uses the existing `hr.Policies`,
append-only `hr.PolicyVersions`, generic approval/decision, audit and idempotency tables. It adds no
mapped entity or migration. Its reserved codes are `HR-FOUNDATION-FIELDS`,
`HR-FOUNDATION-LIFECYCLE` and `HR-FOUNDATION-RECORDS`, with category `EmployeeException` and
rules schema `hr-foundation-policy-v1`. Attendance publication authority does not authorize these policies.

Publication requires `HrFoundationPolicyAuthority` with management's actual approved version,
effective dates and `ApprovedReviewMode = "FixedAdminReview"`. Empty, malformed, unapproved,
future or expired authority configuration returns `hr_foundation_policy_authority_not_configured` (503).
No version, controlled vocabulary, age/probation limit, lifecycle enablement or authority is seeded.
The shared authentication business time zone must also be configured.

All endpoints require current web MFA and current explicit All-scope `hr.foundation-policies.read`.
Proposal writes additionally require `hr.foundation-policies.request`; decisions additionally require
`hr.foundation-policies.review` on a currently effective fixed Admin/SuperAdmin role assignment.
The requester cannot approve, return or reject their own proposal. Custom roles, attendance grants,
historical receipts and matching role names do not establish review authority.

| Method | Route under `/api/v1/hr/foundation-policies` | Behavior |
|---|---|---|
| GET | `/{kind}/current?date=YYYY-MM-DD` | Dated state, definition rowversion and exact effective version identity |
| GET | `/{kind}/history?page=1&pageSize=25` | Bounded immutable publication history |
| GET | `/{kind}/requests?state=Pending` | Bounded request history/queue |
| GET | `/{kind}/requests/{requestId}` | Frozen typed proposal and retained decisions |
| POST | `/{kind}/requests` | Freeze a proposed version for independent review |
| POST | `/{kind}/requests/{requestId}/decision` | Approve and append a version, or return/reject without publication |
| POST | `/{kind}/requests/{requestId}/cancel` | Requester cancels a pending proposal with both expected rowversions and retained reason |

`kind` is `Fields`, `Lifecycle` or `Records`. Writes require the existing 8–100 character
`Idempotency-Key` protocol. Reasons are mandatory printable trimmed text, up to 2,000 characters.
Policy/request expected rowversions are the exact eight-byte Base64 versions returned by reads.
`ExpectedLatestVersion` must equal the current publication count/revision; a first proposal sends
zero and `ExpectedPolicyVersion: null`. Later proposals send the definition's current version.
A review sends both current rowversions and the frozen expected latest version. Rowversions and
the current authorization transaction guard competing writes. Idempotent replay rechecks current
permission, MFA, authority and exact resource eligibility before returning protected saved results.
Requester-only cancellation retains the same current Read/Request/MFA/All-scope and resource checks,
but does not require current publication authority. This permits withdrawal of a stale proposal after
the approved authority window expires; it never publishes rules or grants review authority.

The proposal has `ApprovedVersion`, `EffectiveFrom`, `EffectiveTo`, `Rules` and `Reason`.
`Rules` contains exactly one selected family: `fields`, `lifecycle` or `records`. JSON rejects unknown,
duplicate, case-mismatched and coerced members. New effective dates may be today or future, never
backdated. Published version labels cannot be reused. Publication captures the exact approved authority
version and its date window in the private proposal; changing the authority before review requires a new proposal.
Enum JSON uses exact supported names, including document/identity types and decisions; numeric or
case-mismatched names are rejected. Feature-local converters keep strict validation and ordinary MVC
binding consistent without changing global API serialization.

Field rules contain explicitly entered gender/marital/SIM-status/SIM-credit vocabularies, required-field
flags, optional age/probation limits and identity types requiring expiry. Empty vocabularies do not
silently establish new required values; required gender/marital status needs a nonempty matching list.
Values are trimmed, printable and case-insensitively distinct. SIM credit values are at most 40 characters.
The optional `requiredDocuments` array contains `{ staffCategoryCode, types }` rows. Null category means
global; other categories use their exact immutable uppercase stored code. Category rows and document
types must be distinct; types must be a nonempty list of existing `EmployeeDocumentType` values.
This selects Standard document types and does not grant document download, medical or bank access.
The existing minimum identity/profile-photo checks remain in the employee consumers.

Lifecycle rules explicitly specify `allowRetirement` and `allowSuspensionRelease`. They do not create
rehire employment episodes or broader taxonomy. Record rules support only the explicitly approved
`FixedAdminReview` operational authority mode. These policy APIs do not perform employee transitions,
asset custody, SIM transfer, certification renewal or attendance/payroll changes.

Effective windows are inclusive at `EffectiveFrom` and exclusive at `EffectiveTo`. Successor starts
must increase strictly. An open predecessor ends logically at its successor's start; its saved row is
not updated. An explicitly finite predecessor may touch a successor or leave a gap, but cannot overlap.
The reader selects the latest started successor first, then checks its expiry. A gap or expired successor
never revives an older open-ended version. A future first approved version is unavailable until its start.

`IHrFoundationPolicyReader` exposes `GetFieldsAsync`, `GetLifecycleAsync` and `GetRecordsAsync` for a
selected business date. Each returns `effective`, `none` or `unavailable` with typed rules and exact
policy/version/number/approved-label/effective-window identity when effective. `none` means there is
no actual publication for that kind, including a first pending proposal, and permits configuration-only
baseline compatibility. A publication that is future, expired, inactive, malformed or unapproved is
`unavailable`; consumers must never fall back to configuration in that state. Dedicated approved request
type, subject, stage, independent fixed-role decision, actor/time and private evidence references must
match a saved publication. There is no generic "approved request" or DirectHr shortcut.

Protected private objects retain proposal, decision, audit and actor/action/payload-bound retry state.
Their byte length/SHA-256 and server-selected purpose are verified on reads. Responses expose factual
IDs and typed company policy rules, never private storage paths or keys. Business records, approval,
audit and receipt save inside the existing serializable authorization transaction. This feature creates
no accounts, deployment settings, database data or provider success on its own.

Verification source is [VerifyFoundationPolicies.cs](foundation-policies/VerifyFoundationPolicies.cs).
It runs real Application services and pure resolution with synthetic repositories, current grants and
private bytes in a temporary standalone .NET harness. No permanent test project is added. It does not
prove SQL range locks, database rollback/restart, authenticated HTTP/CSRF or real private storage/scanner
acceptance. Actual policy values and operational acceptance remain explicit dependencies.
The separate [query harness](foundation-policies/VerifyFoundationPolicyQueries.cs) translates the actual
bounded repository query using SQL Server EF metadata with a connection-rejecting interceptor. It checks
dedicated approval scope, actor/time/reference evidence and existing revision concurrency; it executes no SQL.
