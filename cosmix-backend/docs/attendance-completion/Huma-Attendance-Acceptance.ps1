<#
Prepared acceptance CONTRACT only. No approved Huma attendance fixture manifest was supplied.
This file performs no SQL/HTTP, private-path inspection, secret read, build, host operation,
provisioning or filesystem write. It does not load or execute any referenced runner.

Root owns existing runners, migrations, shared registrations and all subsequent execution.
The existing Cosmix_Testing has eight additional drafts with unresolved provenance. Counts,
manifest hashes and approval booleans do not establish human consent or synthetic ownership.
Do not run an initializer against it. This contract is the explicit fallback requested when
fixture-domain approval is absent; case descriptions are NOT implemented or passed HTTP tests.

Interface:
  ./docs/attendance-completion/Huma-Attendance-Acceptance.ps1
  ./docs/attendance-completion/Huma-Attendance-Acceptance.ps1 -AsJson
  ./docs/attendance-completion/Huma-Attendance-Acceptance.ps1 -CaseId SUMMARY-RACE

Fresh alternative: separately approve a NEW LocalDB instance, retaining the guarded catalog
name Cosmix_Testing in that instance, new private storage/keys and newly generated credentials.
LocalTesting.cs permits explicit local pipes but pins the catalog and three synthetic account
triples. Huma-Acceptance-Common.ps1 pins the OLD pipe, owner, roots and 11/8 inventory; it cannot
execute this alternative. Root must prepare/review its adapter without bypassing either guard.
Start-LocalTesting.ps1 reads source user secrets and builds; it is not a usable isolation adapter.
Never copy source credentials, settings, ciphertext, grants or keys to the fresh instance.

SRS anchors below are one-based w:p positions under w:body in word/document.xml, including
table and empty paragraphs, obtained by structured ZIP/XML extraction, not page numbers.
#>
[CmdletBinding()]
param(
    [ValidateSet('AUTH-SCOPE','SELF-SCOPE','HISTORICAL-SCOPE','SUMMARY-RETRY','SUMMARY-RACE',
        'SUMMARY-DURABLE','POLICY-PUBLISH','LEAVE-REVIEW','LEAVE-UNITS','LEAVE-RACE',
        'LEAVE-EVIDENCE','REPORTS-DURABLE','INBOX-RACE','FINANCE-EVIDENCE','MONTH-BLOCKERS')]
    [string]$CaseId,
    [switch]$AsJson
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$cases = @(
    [ordered]@{
        Id = 'AUTH-SCOPE'; MaximumHttpRequests = 8; MaximumBusinessWrites = 0
        Routes = @('GET /api/v1/me/permissions', 'GET /api/v1/leaves/admin/queue')
        Fixtures = @('Actors', 'ExactGrants', 'OwnedHostReceipt')
        Assertions = @(
            'Use New-LocalAcceptanceSession for password + browser-bound local OTP + refreshed CSRF; no fabricated bearer/cookie.'
            'Compare each flat PermissionGrant tuple: permission, sensitive, roleAssignmentId, scopeKind, targetId. A 200 alone proves no grant or scope.'
            'Read queue with a distinct current Admin/SuperAdmin assignment carrying attendance.leaves.review with All scope; HR without that assignment receives 403.'
            'Separate permission-on-custom-role and fixed-role-only actors must fail review; do not grant permissions merely to make a negative probe pass.'
        )
        Persistence = 'Root verifier binds actual session, challenge, delivery, account and audit IDs to approved login budgets; no password/OTP/cookie/CSRF logging.'
    }
    [ordered]@{
        Id = 'SELF-SCOPE'; MaximumHttpRequests = 8; MaximumBusinessWrites = 0
        Routes = @('GET /api/v1/me/attendance/daily?businessDate={date}', 'GET /api/v1/leaves/mine/{id}', 'GET /api/v1/me/notifications/{id}')
        Fixtures = @('Actors', 'SelfEmployee', 'ForeignLeaveRequest', 'ForeignNotification')
        Assertions = @(
            'Assert returned employeeId equals the session-linked synthetic employee; supply no client employee identity.'
            'Known foreign leave and notification IDs receive 404, with no foreign payload. Compare a well-formed unknown ID.'
            'A denied management actor receives 403 on the separately scoped daily route; self permission does not imply management permission.'
        )
        Persistence = 'Read auditing is budgeted; no leave, session, summary or notification transition is expected.'
    }
    [ordered]@{
        Id = 'HISTORICAL-SCOPE'; MaximumHttpRequests = 8; MaximumBusinessWrites = 0
        Routes = @('GET /api/v1/attendance/employees/{employeeId}/daily?businessDate={date}', 'GET /api/v1/attendance/exceptions?from={from}&to={toExclusive}&scope=3&targetId={siteId}', 'GET /api/v1/attendance/employees/{employeeId}/sessions?from={from}&to={toExclusive}')
        Fixtures = @('AllowedEmployeeDay', 'DeniedEmployeeDay', 'TransferTimeline', 'OriginalSourceSites')
        Assertions = @(
            'Allow only the expressly granted historical employee/date/source-site combination; assert exact returned IDs and dates.'
            'Denied employee must have valid historical employment AND schedule so 403 tests scope. A 409 employee_assignment_unavailable is fixture failure, not successful denial.'
            'Transferred and overnight sources require all original retained sites/dates. A current-site grant cannot authorize an old source.'
            'Cursor binding is rechecked for actor, employee, purpose and exact filters; session audit revisions are not summed as separate duty.'
        )
        Persistence = 'Protected source memberships and current grants are independently compared by root; no history is backdated.'
    }
    [ordered]@{
        Id = 'SUMMARY-RETRY'; MaximumHttpRequests = 8; MaximumBusinessWrites = 1
        Routes = @('POST /api/v1/attendance/employees/{employeeId}/daily/recalculate', 'GET /api/v1/attendance/employees/{employeeId}/daily/history?businessDate={date}')
        Fixtures = @('FreshSummaryDay', 'ExactExpectedRevision', 'ApprovedReason', 'RunPrefixedKeys')
        Body = '{businessDate,expectedRevision,reason}; exact complete request retained in private evidence'
        Assertions = @(
            'One authorized save creates the expected revision; exact same-key/body retry returns the same id and revision.'
            'Changed reason under that key returns 409 idempotency_key_reused; fresh key with stale expectedRevision returns 409 attendance_summary_revision_conflict.'
            'Missing CSRF fails before a business mutation; explicitly record observed CSRF status from the existing middleware rather than guessing an error code.'
            'Assert policy status and nulls from supplied real sources. A factual no-duty day is neither absence nor payroll success.'
        )
        Persistence = 'Exactly one summary append, matching session links, input envelope, committed audit and completed protected retry receipt; unchanged raw evidence.'
    }
    [ordered]@{
        Id = 'SUMMARY-RACE'; MaximumHttpRequests = 6; MaximumBusinessWrites = 1
        Routes = @('POST /api/v1/attendance/employees/{employeeId}/daily/recalculate')
        Fixtures = @('SeparateFreshSummaryDay', 'TwoIndependentAuthorizedSessions', 'ExactExpectedRevision', 'DifferentApprovedReasons')
        Assertions = @(
            'Start two requests behind a bounded barrier with independent connections and browser/CSRF contexts; never share a mutable WebRequestSession across threads.'
            'Different keys/bodies at one expected revision yield one winning revision and one 409 attendance_summary_revision_conflict.'
            'Exact concurrent same-key/body retry is a SEPARATE fresh fixture: both return the same committed identity, with no second revision.'
            'Record start/completion/correlation IDs and actual overlap; serial requests cannot be labelled a race.'
        )
        Persistence = 'Root SELECT verifier observes one committed revision/receipt for the successful operation, no loser business row, and unchanged source rows after both connections finish.'
    }
    [ordered]@{
        Id = 'SUMMARY-DURABLE'; MaximumHttpRequests = 4; MaximumBusinessWrites = 0
        Routes = @('GET /api/v1/attendance/employees/{employeeId}/daily?businessDate={date}', 'POST /api/v1/attendance/employees/{employeeId}/daily/recalculate')
        Fixtures = @('CommittedSummaryAndKey', 'OriginalBody', 'RootOwnedRestartReceipt', 'PrivateStorageVerifier')
        Assertions = @(
            'Root stops/restarts only its owned PID with matching start time, same reviewed assemblies/configuration and same fresh-run keyring.'
            'After real MFA in a fresh browser, reauthorize and replay the original key/body within the 24-hour receipt window; exact identity/provenance must survive restart.'
            'Independent protected reads verify input, audit and receipt hashes/purposes/employee/date; a successful GET alone is not durable encrypted-storage acceptance.'
        )
        Persistence = 'Retain original encrypted objects and receipt; verify no duplicate summary or charge. Restart and key reads belong only to separately approved root execution.'
    }
    [ordered]@{
        Id = 'POLICY-PUBLISH'; MaximumHttpRequests = 10; MaximumBusinessWrites = 3
        Routes = @('POST /api/v1/hr/attendance-policies', 'POST /api/v1/hr/attendance-policies/{id}/publication-preview', 'POST /api/v1/hr/attendance-policies/{id}/versions', 'POST /api/v1/hr/attendance-policies/{id}/assignments', 'GET /api/v1/hr/attendance-policies/effective/{employeeId}/{date}')
        Fixtures = @('ApprovedSchema3Rules', 'FuturePakistanEffectiveInterval', 'ExactTargetPopulation', 'PublisherGrants', 'UnchangedCompensationBaseline')
        Assertions = @(
            'Direct permissioned HR publication uses expectedVersion/revision and exact preview fingerprint; a role label alone grants nothing.'
            'Incomplete/invalid rules preserve the prior applicable policy and compensation; validation returns published=false.'
            'Assign only the approved population. Conflicting populations remain conflict, without invented precedence.'
            'Future rules cannot prove an earlier historical calculation; use actual elapsed time or separately approved synthetic historical construction, labelled as such.'
        )
        Persistence = 'Definition, immutable version and applicability plus approved audits/receipts; no salary update, automatic grant, legal activation or posting.'
    }
    [ordered]@{
        Id = 'LEAVE-REVIEW'; MaximumHttpRequests = 12; MaximumBusinessWrites = 5
        Routes = @('POST /api/v1/leaves/mine', 'GET /api/v1/leaves/admin/{id}/policies', 'POST /api/v1/leaves/admin/{id}/decisions', 'POST /api/v1/leaves/mine/{id}/cancellations', 'POST /api/v1/leaves/admin/{id}/cancellations/{cancellationId}/decisions')
        Fixtures = @('ExactFutureShiftIntervals', 'LeaveType', 'ApplicableLeavePolicy', 'DifferentSubmitterBeneficiaryAndReviewer', 'ApprovalRowversions', 'CancellationRowversion')
        Assertions = @(
            '201 intake is pending review; Paid approval uses decision=1,treatment=1, explicit policyVersionId and expectedApprovalVersion.'
            'Requester and beneficiary cannot decide, including same-key replay; custom-role HR permission alone cannot replace the fixed Admin assignment requirement.'
            'Return/reject use treatment=0 and policyVersionId=null; returned resubmission retains previousRequestId and original evidence.'
            'Cancellation is a distinct Admin decision; it preserves the approval and appends only the exact utilization reversal.'
            'Casual approval precedes the first interval; locked dates block approval/cancellation rather than rewriting frozen evidence.'
        )
        Persistence = 'Manifest selects exact branches and deltas. Intake/approve/cancel-request/cancel-decision uses four writes. Return/resubmit/review requires its own separately approved request fixtures and budget, not an extra action on the approved request.'
    }
    [ordered]@{
        Id = 'LEAVE-UNITS'; MaximumHttpRequests = 6; MaximumBusinessWrites = 0
        Routes = @('GET /api/v1/leaves/mine/balance?leaveTypeId={type}&policyVersionId={policy}&asOf={date}', 'POST /api/v1/leaves/mine/preview')
        Fixtures = @('ProtectedHistoricalUtilizationsAndReleases', 'Original480And540MinutePolicyUnits', 'FiscalBoundaryExamples', 'ExplicitExpectedFractions')
        Assertions = @(
            'July-June year and 18/6/12 quotas use exact numerator/denominator units from each immutable original policy.'
            'Twelve approved 480-minute days remain twelve days under a 540-minute policy; reversing one old day restores exactly one day.'
            'No selected policy or missing unit provenance produces null unavailable/conflict balances, not zero.'
            'Balance AsOf selects applicability/year, but usage is the currently recorded ENTIRE fiscal-year ledger, including future approvals; it is not a historical snapshot.'
            'Preview approvalGuaranteed stays false and creates no leave/ledger/approval/private object/receipt; ordinary read audit is still budgeted.'
        )
        Persistence = 'Require actual approved history, never insert Entitlement/Encashment rows expecting the Utilization/Release-only quota query to consume them.'
    }
    [ordered]@{
        Id = 'LEAVE-RACE'; MaximumHttpRequests = 6; MaximumBusinessWrites = 1
        Routes = @('POST /api/v1/leaves/admin/{id}/decisions', 'POST /api/v1/leaves/admin/{id}/cancellations/{cancellationId}/decisions')
        Fixtures = @('TwoIndependentReviewerSessions', 'SeparatePendingApprovalFixture', 'SeparatePendingCancellationFixture', 'ExactExpectedApprovalVersions', 'RootApprovedFailureCodes')
        Assertions = @(
            'Concurrent different-key decisions over the same pending approval permit exactly one terminal decision and one matching utilization.'
            'A distinct cancellation race permits exactly one terminal cancellation and exactly one equal reversal referencing its utilization.'
            'Manifest selects ONE race per fresh fixture. A stale valid eight-byte approval rowversion returns 409 leave_version_conflict in LeaveRules.Version; generic any-409 is insufficient.'
            'Exact same-key/body concurrency and changed-body conflict use separate approved fixtures, bounded connections and no retry-on-conflict loop.'
        )
        Persistence = 'Root verifies atomic approval/request/day/ledger/audit/receipt state via separate SELECT-only connection. Losing transaction may leave encrypted orphans; record them without cleanup.'
    }
    [ordered]@{
        Id = 'LEAVE-EVIDENCE'; MaximumHttpRequests = 6; MaximumBusinessWrites = 1
        Routes = @('POST /api/v1/leaves/mine', 'GET /api/v1/leaves/mine/{id}/evidence/{kind}', 'GET /api/v1/leaves/admin/{id}/evidence/{kind}')
        Fixtures = @('ApprovedSyntheticEvidenceBytesAndSha256', 'ActualScannerConfigurationApproval', 'RequiredEvidenceKind', 'PrivateStorageVerifier')
        Assertions = @(
            'Sick medical, exam admit-card/date-sheet, emergency half-day and injury-rest evidence requirements are distinct supplied cases.'
            'Unavailable/non-Clean scanner cannot create successful leave evidence. Do not substitute a scanner double or forged Clean result in runtime DI.'
            'Authorized own/Admin downloads match original bytes, size, media type and digest; foreign IDs disclose no attachment or private path.'
            'Emergency half-day without evidence is blocked; no one-percent KPI event is asserted because that producer is absent.'
        )
        Persistence = 'One selected clean case at most; other evidence kinds and scanner faults need separate fixture approval. Protected original bytes/metadata survive root-owned restart.'
    }
    [ordered]@{
        Id = 'REPORTS-DURABLE'; MaximumHttpRequests = 56; MaximumBusinessWrites = 4
        Routes = @('POST /api/v1/attendance/reports/exports', 'GET /api/v1/attendance/reports/exports/{id}', 'GET /api/v1/attendance/reports/exports/{id}/download')
        Fixtures = @('SingleEmployeeSingleDay', 'FourExactExportBodiesAndKeys', 'ExplicitWorkerApproval', 'TwoAdminSessionsAndOneDeniedHrSession', 'RootLeaseAndRestartVerifier')
        Assertions = @(
            'Disabled worker returns 503 attendance_export_worker_not_enabled; positive generation needs separately approved effective private configuration.'
            'CSV=1, XLSX=2, PDF=3 requests create three usable formats; a fourth CSV bound to a newly logged-out generation session fails without output.'
            'Two concurrent identical CSV requests use independent connections and resolve one job; changed-body same-key conflicts and missing CSRF cannot queue a job.'
            'Root holds the production claim application lock before queuing, stops the owned host, then exercises actual production repository claim and natural lease expiry without editing timestamps.'
            'After restart, completed files retain exact employee/date/filter/generation/lock/source metadata and factual-not-payroll meaning. Parse real bytes; the JSON-only HTTP helper cannot download binary files.'
        )
        Persistence = 'Proposed upper bounds: 4 exports, 7 outbox rows, 3 StoredFiles, 3 notifications, 4 completed request receipts, 21 protected objects. Root must approve exact deltas; a Failed outbox may leave the business job Pending, projected failed by status API.'
    }
    [ordered]@{
        Id = 'INBOX-RACE'; MaximumHttpRequests = 24; MaximumBusinessWrites = 3
        Routes = @('GET /api/v1/me/notifications', 'GET /api/v1/me/notifications/{id}', 'POST /api/v1/me/notifications/{id}/read')
        Fixtures = @('ThreeRunOwnedReportReadyNotifications', 'ActualRowversions', 'TwoIndependentRecipientSessions', 'DeniedRecipient')
        Assertions = @(
            'Another recipient gets 404; notification/job binding is independently verified from protected intents, not inferred from display text.'
            'Valid but stale unread expectedVersion returns 409 notification_version_conflict, without setting readAt.'
            'Two concurrent actual-version mark-read calls produce one changed=true and one changed=false with the same preserved readAt.'
            'Replay with original version keeps readAt and creates no second read transition/audit; missing CSRF is denied.'
        )
        Persistence = 'Exactly three new read transitions at most, no changes to existing inbox rows, no duplicate notifications after approved idle/restart observation.'
    }
    [ordered]@{
        Id = 'FINANCE-EVIDENCE'; MaximumHttpRequests = 10; MaximumBusinessWrites = 3
        Routes = @('GET /api/v1/attendance/employees/{employeeId}/payroll-inputs/{year}/{month}/revisions/{id}', 'POST /api/v1/attendance/employees/{employeeId}/payroll-inputs/{year}/{month}/revisions/{id}/attestations')
        Fixtures = @('RealProtectedDeductionBearingSource', 'OwnerApprovedSyntheticFinanceActorAndScopes', 'ExactLineKeysAmountsCurrency', 'ExplicitSyntheticExternalEvidence', 'ExpectedPredecessors')
        Assertions = @(
            'Only explicitly assigned Finance attest permission AND source-read permission authorize the actor; HR/Admin titles imply nothing.'
            'Handoff does not confirm posting; posted/settled remain manual_authorized_attestation with payrollReady=false,paymentInstruction=false.'
            'Exact source revision/fingerprint, predecessor, lines, amounts, currency, evidence digest and UTC times are revalidated; changed key payload/stale predecessor/one-cent mismatch fails.'
            'Empty valid source returns 409 payroll_attestation_no_deduction_lines; absent/unavailable is not zero.'
            'Actual external posting is not proved by a synthetic manual assertion. Never fabricate an external receipt to clear financial readiness.'
        )
        Persistence = 'Three immutable chain rows maximum for ONE deduction-bearing source, protected evidence/audit/receipts and SQL uniqueness. Positive cases unavailable until real approved source fixture exists.'
    }
    [ordered]@{
        Id = 'MONTH-BLOCKERS'; MaximumHttpRequests = 4; MaximumBusinessWrites = 0
        Routes = @('GET /api/v1/attendance/month-locks/{year}/{month}/preview', 'POST /api/v1/attendance/month-locks/{year}/{month}/finalize')
        Fixtures = @('ExplicitAllScopeReadAndFinalizeActors', 'ExactMonthPopulationAndIssues', 'ExpectedPreviewFingerprintAndVersion')
        Assertions = @(
            'Site/employee-only grants cannot operate a global month. Missing assignment is an employee/day readiness issue in current code.'
            'Use current preview fingerprint/version. Known incomplete sources cause ready=false and 409 attendance_month_not_ready on finalize.'
            'Current financial reader retains legal_payroll_activation_required and explicit missing-source/handoff issues; approved OT and factual-only carry-forward blockers cannot become verified zero or a ready lock.'
            'Unexpected authorization/storage/provenance failures propagate; do not label all 409 responses an expected readiness blocker.'
            'No positive finalize case is prepared without the actual integrated financial reader and approved complete month fixtures. Finalized months never reopen.'
        )
        Persistence = 'No period/lock event/membership or fabricated financial manifest is expected. Required attempts/audits must be explicitly budgeted.'
    }
)

$contract = [ordered]@{
    SchemaVersion = 1
    Profile = 'Huma-attendance-required-fixture-contract-v1'
    Status = 'PreparedContractOnly'
    HttpExecuted = $false; SqlExecuted = $false; SecretsRead = $false
    AcceptanceEstablished = $false; ExecutionSupported = $false
    TargetOptions = @(
        'ExistingDatabase: all 11 exact employee IDs (including eight drafts), rowversions, synthetic provenance and owner approvals; preserve retained key dependencies and obtain separate write approval.'
        'FreshIsolatedLocalDbInstance: explicitly approved new instance/pipe with independent Cosmix_Testing, fresh storage/keys/credentials and supported synthetic setup. Old database and private roots are not inputs.'
    )
    ExistingRunnerInterfaces = @(
        'Huma-Acceptance-Preflight.ps1: ManifestPath, ApprovedManifestSha256, SourceServer, Mode Inspect(default)/Inventory/Preflight, Stage Baseline/Target. Only Inspect is offline; do not invoke SQL modes in this audit.'
        'Huma-Acceptance-Upgrade.ps1: same identity/hash inputs, Mode Inspect(default)/Execute, ActionApproval UpgradeExistingCosmixTesting. Existing-target-only backup/pending-range path, root owned.'
        'Huma-Acceptance-Auth.ps1: same inputs, Mode Inspect(default)/Execute, ActionApproval RealSyntheticWebMfa. Executes SQL preflight, private credential reads and authentication writes; returns no live client.'
        'LocalAcceptanceHttp.ps1: New-LocalAcceptanceSession(BaseUri,Account); Invoke-LocalAcceptanceRequest(Client,Method,Path,Body,Headers,WithoutCsrf). Reuse for actual MFA and sequential JSON checks only after root approvals.'
        'Invoke-AttendanceWebAcceptance.ps1 / Run-AttendanceAcceptance.ps1 / VerifyIsolatedAttendancePersistence.cs: retained source-machine/run fixtures; do not execute unchanged on Huma or use their old journals as fresh evidence.'
    )
    RequiredManifest = [ordered]@{
        Identity = 'SchemaVersion, Profile, unique RunId, exact repository, target mode, human ApprovalReference and scoped approval evidence; placeholders/booleans alone grant nothing.'
        Target = 'Explicit integrated-auth DataSource/actual ServerName/owner/operator/Database; fresh instance identity must differ from Software-PC1\LOCALDB#842DD4FB. No alias fallback, attach, rename, reset or source catalog write.'
        Migration = 'Complete ordered MigrationId/ProductVersion chain and source/designer/snapshot hashes, exact target count/final ID, reviewed SQL artifact hash and rehearsal receipt. Existing baseline 12 needs pending-only range; fresh target needs separately reviewed full chain. Root owns both.'
        Runtime = 'Exact Release/runtime/helper/runner hashes, loopback HTTPS URI, trusted certificate, owned process PID/start/assembly/configuration receipt, effective configuration review and no existing listener takeover.'
        PrivateSetup = 'New approved private roots and restricted ACL receipt for fresh mode, provisioned-key references and key dependency review, no copied source credentials/keys/settings; non-secret manifest contains no key bytes, passwords, OTPs, cookies, bearer or CSRF material.'
        Actors = 'Exact synthetic AccountId/EmployeeId/email linkage and provenance, fixed-role assignment IDs, full expected PermissionGrant tuples/scopes, independent requester/beneficiary/reviewer identities, approved Finance recipients where applicable. No new login identities beyond guarded three without root design.'
        Domain = 'Exact employee hiring/category/status timelines, historical site/shift/roster/geofence/holiday versions, policy/leave types and immutable rule hashes, dated salary only for expressly approved financial cases, original source-site bindings, explicit UTC intervals and Pakistan dates.'
        Cases = 'Selected case IDs only; each needs exact fixture IDs/provenance, baseline revision/rowversion, request bodies/digests/keys, expected status/code/result/source identities, exact allowed row/private-object deltas and distinct subcase fixtures. Do not sum per-case maxima as approval.'
        Budgets = 'Explicit total HTTP/login/OTP/session/audit/business/outbox/file/object limits, 30-second request timeout, at most two race connections, 10 pagination pages, 60-second worker/restart deadline, no unbounded polling/retries or lost-response fresh-key replay.'
        Concurrency = 'Two independently authenticated browser contexts and dedicated connections with bounded start barrier and correlation/timing evidence; use a separate fresh fixture for each race/retry variant. Existing Invoke-WebRequest helper mutates headers and must not share one live session across threads.'
        Storage = 'Root-owned SELECT-only verifier using separate connection, production encrypted-store purpose/hash verification and unchanged source inventory, planned owned-host restart, approved fault scenario if needed, orphan/recovery evidence policy. No direct business SQL writes or key deletion for faults.'
        Workers = 'Explicit per-case approval for AttendanceJobs/reminders/zones/storage/retention. Default all Attendance workers/monitoring off; actual OTP delivery remains necessary. Existing lifecycle worker cannot be disabled by an invented setting, so reconcile pending status changes before any host.'
        Receipts = 'Separate setup/preflight/schema/host/fixture approval receipts and hashes, private CreateNew stage marker/append-only journal owned by root, exact run/correlation/row bindings, stopped/completed/blocked outcomes and retain-on-failure recovery. No automatic rerun, restore, downgrade or cleanup.'
    }
    FreshManifestTemplate = [ordered]@{
        SchemaVersion = 1
        Profile = 'Huma-fresh-isolated-attendance-v1-proposed'
        RunId = $null
        Repository = 'C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend'
        Target = [ordered]@{
            Mode = 'FreshIsolatedLocalDbInstance'
            Database = 'Cosmix_Testing'
            InstanceName = $null; SourcePipe = $null; ActualServerName = $null
            Owner = $null; Operator = $null; IntegratedSecurity = $true
            NewInstanceProvisioningApprovalReference = $null
            EmptyTargetInventoryReceiptSha256 = $null
            OldInstanceAndPrivateRootsExcluded = $true
        }
        PrivateSetup = [ordered]@{
            ArtifactRoot = $null; PrivateRoot = $null; SettingsPath = $null
            FilesRoot = $null; SnapshotsRoot = $null; KeysRoot = $null
            CredentialsPath = $null
            CredentialOrigin = 'NewLocalApprovedProvisioning'
            CredentialProvisioningApprovalReference = $null
            CredentialProvisioningReceiptSha256 = $null
            CredentialsReadableBy = 'ApprovedRootOperatorOnly'
            KeysOrigin = 'NewLocalApprovedProvisioning'
            KeyProvisioningApprovalReference = $null
            KeyReferences = @()
            SourceCredentialsCopied = $false; SourceKeysCopied = $false
            SourceSettingsCopied = $false; SourceCiphertextCopied = $false
            AclAndIsolationReviewReceiptSha256 = $null
        }
        RootAdapters = [ordered]@{
            SetupRunnerPath = $null; SetupRunnerSha256 = $null
            HttpRunnerPath = $null; HttpRunnerSha256 = $null
            SqlPrivateEvidenceVerifierPath = $null; SqlPrivateEvidenceVerifierSha256 = $null
            HttpHelperPath = 'docs/access-provisioning/LocalAcceptanceHttp.ps1'
            HttpHelperSha256 = $null
            OldHumaAuthSupportsFreshSetup = $false
            OldRetainedRunnerExecutionAllowed = $false
        }
        Approvals = [ordered]@{
            ApprovalReference = $null
            FreshInstance = $false; FreshKeys = $false; FreshCredentials = $false
            ExactSyntheticFixtures = $false; ExactAuthenticatedWrites = $false
            Workers = $false; Restart = $false; PrivateEvidenceReads = $false
        }
        CompleteMigrationHistory = @(); ArtifactHashes = [ordered]@{}
        OwnedHostReceiptSha256 = $null; FixtureReceiptSha256 = $null
        Actors = @(); ExactGrants = @(); DomainFixtures = @(); SelectedCases = @()
        TotalBudgets = [ordered]@{}
        CaseEntryShape = [ordered]@{
            CaseId = $null; Variant = $null; FixtureIds = @()
            FixtureProvenanceApprovalReference = $null
            BaselineRevisionOrRowversion = $null
            ExactNonSecretRequestBody = $null; RequestBodySha256 = $null
            RunPrefixedIdempotencyKey = $null
            ExpectedHttpStatus = $null; ExpectedProblemCode = $null
            ExpectedIdentityAndSourceBindings = [ordered]@{}
            ExpectedTableDeltas = @(); ExpectedPrivateObjectDeltas = @()
            MaximumHttpRequests = $null; MaximumBusinessWrites = $null
            ConcurrentSessionAliases = @(); DurableVerifierReceiptKind = $null
        }
        UsableForExecution = $false
        MissingValues = 'All nulls/empty arrays are unresolved. This template is not a supplied fixture manifest or a write approval; this script cannot execute it.'
    }
    ApprovalSequence = @(
        'Root reviews this contract and selects existing ownership-confirmed target OR a separately approved fresh instance setup.'
        'Root prepares exact setup adapter, schema artifacts, fixture plan and non-secret manifest; reviews supported API/date/authority/scanner dependencies and bounded deltas.'
        'Human approval covers exact fresh instance/keys/credentials/fixtures/host and separately the selected authenticated writes, workers, restart and private evidence verification.'
        'Root commissions the approved setup without old-script reuse, pins real fixture identities/versions, then implements/reviews the selected executable HTTP cases.'
        'Only a separately authorized run can produce executed acceptance receipts; absent fixtures/producers remain blocked, never skipped-and-passed.'
    )
    ReadHandoffDocuments = @(
        'AGENTS.md', 'README.md', 'docs/Developer-Setup.md', 'docs/Work-Ownership.md',
        'docs/Architecture.md', 'docs/SRS-Module-Map.md', 'docs/Employee-Fields.md',
        'docs/Local-Testing.md', 'docs/requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx (ZIP word/document.xml, namespace-aware XML)',
        'docs/attendance-completion/Laptop-Continuation-20261012.md',
        'docs/attendance-completion/Continuation-20261012.md',
        'docs/attendance-completion/Approved-Workflow-Continuation.md',
        'docs/attendance-completion/Implementation.md', 'docs/attendance-completion/Verification.md',
        'docs/attendance-completion/Review-20261012.md', 'docs/attendance-completion/Attendance-UAT-Guide.md',
        'docs/attendance-completion/Huma-Acceptance-Setup.md',
        'docs/attendance-completion/Isolated-Acceptance-Plan.md',
        'docs/attendance-completion/Narrow-Web-Acceptance.md',
        'docs/attendance-completion/Extended-Acceptance-Plan.md',
        'docs/attendance-completion/SRS-Remaining-Decisions-20261012.md',
        'docs/attendance-completion/Policy-Integration-Contract.md',
        'docs/attendance-completion/Policy-Leave-Implementation.md',
        'docs/attendance-completion/Policy-Leave-Client-Examples.md',
        'docs/attendance-completion/Direct-HR-Policies.md', 'docs/attendance-completion/Leaves.md',
        'docs/attendance-completion/Payroll-Inputs-Implementation.md',
        'docs/attendance-completion/Payroll-Attestations-Implementation.md',
        'docs/attendance-completion/Legal-Review-Evidence-Implementation.md',
        'docs/attendance-completion/Legal-Applicability-Research-20261012.md (local record only; law not reverified)',
        'docs/attendance-completion/Company-Settings.md', 'docs/attendance-completion/Overtime.md',
        'docs/attendance-completion/Summaries.md', 'docs/attendance-completion/MonthLocks.md',
        'docs/attendance-completion/Exceptions.md', 'docs/attendance-completion/Audit-Reads.md',
        'docs/attendance-completion/Site-Reports.md', 'docs/attendance-completion/Missing-Checkout-Reminders.md',
        'docs/attendance-completion/notifications/README.md',
        'docs/attendance-completion/notifications/Reports.md',
        'docs/attendance-completion/notifications/Verification.md',
        'docs/attendance-policies/Daily-Calculation.md', 'docs/attendance-policies/Payroll-Inputs.md'
    )
    AuditSourceReferences = [ordered]@{
        HumaAuthCredentials = 'docs/attendance-completion/Huma-Acceptance-Auth.ps1:98 (retained current Huma Test-Accounts.json, not new credential provisioning)'
        HumaAuthCoverage = 'docs/attendance-completion/Huma-Acceptance-Auth.ps1:114-119 (real MFA session and 200 permissions only; no exact grant assertions)'
        HumaExistingOnly = 'docs/attendance-completion/Huma-Acceptance-Common.ps1:57-66,231-247 (old pipe/private roots/11 employees/eight drafts/ownership approvals)'
        FreshGuard = 'src/Cosmix.Api/Development/LocalTesting.cs:37-44 (same catalog plus allowed explicit local pipe),56-68 (complete schema and exactly three synthetic triples)'
        FreshSetup = 'src/Cosmix.Api/Development/LocalTestingSetup.cs:35-62 (existing guard, fresh migrations, generated credentials, empty-target transaction); root-only after exact target approval'
        GrantShape = 'src/Cosmix.Application/Authentication/Access/AccessContracts.cs:9-10 (flat permission,sensitive,roleAssignmentId,scopeKind,targetId)'
        KpiAndWaiting = 'src/Cosmix.Domain/HR/Policies/AttendancePolicyRules.cs:10-15 (no KPI/waiting schema); src/Cosmix.Domain/HR/PolicyEffects/PolicyImpactAssessment.cs:13; WaitingTimeEvidence.cs:13 (models, no Application/API producer found)'
        LeaveWorkflow = 'src/Cosmix.Application/HR/Leaves/LeaveService.cs:16,121,164-169,200,219-228 (intake, direct decision, utilization, separate cancellation/reversal)'
        LeaveAuthority = 'src/Cosmix.Application/HR/Leaves/LeaveReviewAuthority.cs:9-23; LeaveService.cs:241-242 (fixed assignment plus permission/All scope; requester/beneficiary denial)'
        LeaveLimits = 'src/Cosmix.Application/HR/Leaves/LeavePolicySettings.cs:21-22,43-44 (18/6/12, four months, July-June); src/Cosmix.Infrastructure/Repositories/HR/LeaveRepository.cs:140-144 (Utilization/Release only)'
        EmergencyKpiGap = 'src/Cosmix.Application/HR/Leaves/LeaveTreatmentRules.cs:9-14 (evidence denial; no KPI effect)'
        EncashmentGap = 'src/Cosmix.Domain/HR/Leaves/LeaveEncashmentRequest.cs:11-28,49-56 (proposed inputs and generic pending submission, no eligibility/valuation service)'
        RecoveryGap = 'src/Cosmix.Domain/HR/Leaves/LeaveRecoveryCommitment.cs:12-22 (commitment only)'
        OfficeScheduleGap = 'src/Cosmix.Application/Attendance/Rosters/SiteRosterResolver.cs:13-18 (site_not_assigned; no invented office shift)'
        FinancialWorkInProgress = 'src/Cosmix.Application/DependencyInjection.cs:68 now registers AttendanceMonthLockFinancialReader. src/Cosmix.Application/Attendance/PayrollInputs/AttendanceMonthLockFinancialReader.cs:24-51 inspects saved source/handoff/jurisdiction and unconditionally retains legal_payroll_activation_required; root integration appeared during audit, not verified by this task.'
        PostingReader = 'src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollAttestationService.cs:92; src/Cosmix.Application/DependencyInjection.cs:69 (manual confirmed-reader exists); not proof of general payroll integration'
        CrossMonthGap = 'src/Cosmix.Domain/Attendance/PayrollInputs/AttendancePayrollInputRules.cs:38-40 now accepts prior-settlement evidence; :121 retains missing/conflicting evidence blockers. AttendancePayrollInputProducer.cs:76 still called the two-argument overload at final inspection. Root wiring and acceptance are in progress, not absent rules or finished reconciliation.'
        GeneralHandoffGap = 'src/Cosmix.Application/HR/PayrollHandoff/README.md:9 (foundation only); separate Attendance manual-attestation APIs do exist'
    }
    SrsGaps = @(
        'OFF-006 / MD-009 (paragraphs 573-574,1143-1144): KPI model fields exist; no typed KPI rules, score/reset producer, authoritative score evidence or operational workflow.'
        'OFF-005 / SITE-005 / MD-010 (571-572,597-598,1145-1146): WaitingTimeEvidence is a model; pairing, attribution, independent verification, valuation and cap workflow absent.'
        'LEAVE-001..010 (636-655): ordinary leave is implemented, but exact authenticated SQL/scan/storage/race acceptance remains unexecuted here; site-only schedule limits office applicability.'
        'LEAVE-008 (650-651): emergency half-day evidence is enforced; missing-evidence one-percent KPI producer absent.'
        'LEAVE-011 (656-657): six extra days/cash requires grant/election/KPI/valuation/consumption contract. Existing settings enforce 18/6/12; quota reads only Utilization/Release.'
        'LEAVE-012 (658-659): LeaveEncashmentRequest does not validate trusted >=70% KPI, unused balance, pricing, review or settlement; model presence is not an encashment service.'
        'LEAVE-013 (660-661): LeaveRecoveryCommitment records promised minutes; no obligation, fulfillment allocation, deadline/waiver or settlement workflow.'
        'AC-007 / ATT-010: manual financial attestation service exists; general locked payroll handoff and financial carry-forward remain incomplete. Concurrent root financial-reader/reconciliation work must be rechecked before later execution.'
    )
    Cases = $cases
}

if ($CaseId) {
    $result = $cases | Where-Object { $_.Id -ceq $CaseId }
} elseif ($AsJson) {
    $result = $contract
} else {
    $result = [pscustomobject]@{
        Status = $contract.Status
        ExecutionSupported = $false
        HttpExecuted = $false
        SqlExecuted = $false
        AcceptanceEstablished = $false
        PreparedCaseContracts = $cases.Count
        RequiredInput = 'Root-reviewed exact synthetic fixture manifest plus separate setup and execution approvals.'
        Interface = '-AsJson emits the complete contract; -CaseId emits one bounded case. Neither executes acceptance.'
    }
}
if ($AsJson) { ConvertTo-Json -InputObject $result -Depth 12 } else { $result }
