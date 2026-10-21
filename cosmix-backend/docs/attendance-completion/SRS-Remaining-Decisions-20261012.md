# Attendance Extensions: Remaining Decisions and Safe Implementation Scope

Audit date: 12 October 2026. Repository: `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend`.

This is a read-only sidecar audit, with this document as its only output. It records requirements and source-code evidence; it does not approve policy, introduce formulas, or authorize deployment. No shared files, application code, SQL, Git state, build, test project, accounts, providers or runtime settings were changed. No database was connected and no build or behavioral checks were run for this audit.

## 1. Evidence and Interpretation

Read `AGENTS.md`, `README.md`, `docs/Architecture.md`, `docs/SRS-Module-Map.md`, `docs/Work-Ownership.md`, the latest Attendance completion/policy/leave/payroll/company/report records, and the actual Domain, Application, Infrastructure and API code. Extracted the SRS directly from the DOCX ZIP entry `word/document.xml` using XML namespace-aware paragraph/text-node reads; no conversion or rendered document was used.

SRS paragraph references below are one-based positions among all `w:p` nodes under `w:body`, including empty paragraphs and table paragraphs. They are reproducible XML anchors, not page numbers. Source references C01-C19 identify repository files and relevant line numbers in section 8.

Interpretation order: the latest user instruction overrides older SRS ambiguity and historical proposal text; executable code establishes implementation status. A model, enum, FK, generic approval reference, configuration validator or folder is not a delivered workflow. An implemented workflow is also not proof of deployment or real authenticated acceptance.

The latest section of [Verification.md](Verification.md) records migrations 18-20 as prepared, not applied, and an isolated baseline at 17. This audit did not independently inspect database history. Prior build/harness/anonymous HTTP results belong to their recorded runs; they are not fresh evidence from this audit.

The opening supersession notice in [Policy-Integration-Contract.md](Policy-Integration-Contract.md) matters: its later historical body still describes policy/leave as absent and the unavailable calculator as registered. Actual registrations use `ConfiguredAttendancePolicyCalculator`, the real payroll-input producer/service, and jurisdiction reader (C01). The proposal's financial manifest is still a proposal, not an implemented adapter.

## 2. Confirmed Boundaries: Do Not Ask Again

| Confirmed rule | Consequence for this checklist |
|---|---|
| Site-only scheduling; direct permission-controlled HR shift changes | Reuse the historical site resolver. Do not invent office schedules or revive individual scheduling. |
| Ordinary employee leave goes directly to Admin; HR correction and OT reviewer | Preserve these established workflows. Only authority for new benefit/encashment/waiting/KPI/recovery actions remains to be specified where absent. |
| Never reopen a finalized month; use the first later unlocked target | Preserve source locks and exact frozen revisions. Missing financial interpretation cannot be solved by reopening or rewriting history. |
| Ordinary monthly salary inputs use BASIC and actual month days; schema 3 ordinary/late amounts require two decimals | Reuse the existing configured implementation and immutable financial revisions. No new ordinary formula decision is requested. |
| Only the larger overlapping ordinary/late deduction counts | Preserve candidate and suppression explanations. This confirmation does not define interaction with new waiting-loss, KPI or recovery effects. |
| OT uses BASIC, source-month calendar days and nine hours, with final whole-rupee rounding; office OT is ineligible | Consume the retained approved OT award and formula evidence. Do not reprice it using a new salary or borrow the OT rule for encashment. |
| Settlement requires explicit Accounts/Finance-permissioned receipts bound to exact revision, lines, amounts and posting | Handoff alone is not posted. Prepared inputs, approval, factual adjustments and export completion are not settlement. No salary release is authorized. |
| Legal authority is proposed, not formally approved | Preserve legal-review-required status. No configurable rate, company jurisdiction or technical permission establishes formal legal activation. |

The following questions request only remaining extension policy or missing technical contracts. They do not reopen these confirmations.

## 3. Requirement and Implementation Checklist

| Requirement | Current source-grounded status | Remaining work / blocker |
|---|---|---|
| OFF-003 / SITE-003 ordinary late salary inputs | Implemented local schema-3 ordinary/late producer, separately permissioned preview, immutable replacement revisions and protected source evidence (C02-C04) | Cross-month prior-deduction reconciliation, legal approval evidence, pre-lock consumer and posting integration. Saving a revision does not make payroll ready. |
| OFF-006 / MD-009 annual late KPI | Model only: `PolicyImpactAssessment.ProposedKpiPercent` and `LateKpi` kind. Typed attendance schema has no KPI section (C02, C05) | Approved percentage base, annual window/reset, event selection, tier boundaries, score composition, authority and evidence producer. |
| SITE-010 monthly shortage / KPI; OFF-011 make-up/studying policy | Time facts and ordinary shortfall candidates exist; generic `Shortage` / `MakeUpHours` assessment kinds exist (C04-C05) | No dedicated committed-hours/KPI or make-up policy workflow. Exact target, qualifying hours and interactions are unresolved. |
| OFF-005 / SITE-005 / MD-010 paired-worker waiting | `WaitingTimeEvidence` model and EF mapping only (C06); source search found no Application/API workflow | Pair provenance, verification/decision authority, causation, attribution, valuation, overlap and cap rules. |
| LEAVE-008 missing emergency-half-day evidence KPI | Ordinary leave evidence checks are implemented; approval rejects evidence-free emergency half-day. No missing-evidence KPI producer (C07-C08) | SRS says missing evidence may cause a one-percent event; it does not make that automatic. Decide event trigger, remediation and score effect. |
| LEAVE-001 to LEAVE-010 ordinary leave | Implemented local direct Admin review, exact days, fiscal quotas, evidence, cancellation/reversals and self balance/preview (C07-C09) | Operational acceptance remains separate. Do not classify ordinary leave as model-only. Office staff without a site schedule remain unavailable. |
| LEAVE-011 / MD-011 six extra days or equivalent pay at 100% KPI | No benefit grant/election workflow. Current leave settings explicitly validate 18 annual / 6 sick / 12 casual days, not an additional entitlement (C07) | Grant/election eligibility, score evidence, year, day units, cash valuation, authority and expiry/reversal rules. |
| LEAVE-012 unused leave encashment at >=70% KPI | `LeaveEncashmentRequest` and submission method/EF mapping only; no calculator/review/settlement service (C10) | Eligible unused balance, final KPI evidence, valuation, approval action and consumption/posting lifecycle. |
| LEAVE-013 long-leave recovery | `LeaveRecoveryCommitment` model/EF mapping only: dated committed minutes and pre/post flag (C11) | Obligation derivation, qualifying fulfillment, allocation/deadlines, approval/waiver authority and financial treatment. |
| ATT-010 financial month readiness / AC-007 | Direct month-lock service and immutable membership/evidence implemented. Positive financial readiness unavailable (C12-C13) | Daily result stays `PayrollReady=false`; approved OT and factual carry-forward remain blocked. No verified complete financial readiness adapter is wired. |
| MD-014 payroll handoff / confirmed settlement | Batch, lines and processing-record models only; Application folder contains README only (C14-C15) | Actual preparation/receipt consumer, exact revision/line/amount/posting validation, source deduplication and durable reconciliation. |
| SRS 15 / REP-001 to REP-003 relevant reports | Factual Attendance dashboard/register and real bounded CSV/XLSX/PDF job code implemented (C16-C17) | Financial/KPI/waiting/benefit/encashment/recovery registers and payroll reconciliation reports are not implemented; see section 6. |

`LeaveLedgerEntryType` includes Entitlement, Reservation, Adjustment and Encashment. That does not implement those flows: the current quota query reads only Utilization and Release (C09). Adding an extra-day or encashment row alone would not update the current ordinary balance calculation.

## 4. Remaining Decision Records

All examples below are synthetic policy questions, not approved outcomes. No unresolved percentage or monetary result is calculated. Close a record with an approved effective-dated rule/reference and expected outcomes for its examples; a verbal label such as "as per policy" is insufficient.

### D01. KPI Base, Annual Reset and Event Composition

SRS: OFF-006, SITE-010/011, OFF-011, LEAVE-008, MD-009; XML paragraphs 573-574, 583-584, 607-610, 650-651, 1143-1144. Code: C02, C05, C08. Ordinary late counting already has its own configured reset/signature; that is not an approved KPI reset.

| Dimension | Exact unresolved decision |
|---|---|
| Formula/base | What is the authoritative starting KPI and denominator? Do the SRS 0.8% and 0.5% amounts reduce score in percentage points, apply to another performance base, or have another approved meaning? Are they performance-only or monetary? Define composition with shortage, missing-evidence and make-up events, rounding and score bounds. No salary basis is inferred. |
| Count/boundaries | Which dated late classifications count toward KPI: late only, half-day/hourly arrivals, approved corrections, excused lateness, transfer days? Confirm handling at counts 30/31, 60/61, 120 and beyond 120; the SRS disciplinary language is not automatic termination authority. |
| Reset | Is the annual KPI window calendar, July-June or another explicit year? What happens at joining, transfer, policy replacement, rehire and late approval of an old event? The confirmed ordinary late-rule reset does not answer this. |
| Eligibility | Does the office late-tier rule also apply to site staff and which categories? What defines monthly committed hours: retained schedule, approved leave, paid holidays, off days, travel and approved exceptions? What is the exact make-up eligibility for studying employees? |
| Authority/evidence | Who verifies an event, accepts/removes a KPI effect and finalizes the score used for a benefit? Which permission/scope and segregation checks apply? Require exact source date, policy, attendance/correction/leave decision and assessed-score revision; an editable percent or generic approval ID is insufficient. |
| Settlement | If performance-only, no payroll line is implied. If any monetary effect is formally approved, define its component, legal approval evidence and receipt-bound posting; do not reuse the confirmed ordinary deduction formula automatically. |

Concrete questions:

- An employee has late events numbered 30, 31, 60, 61, 120 and 121 under the same applicable rule. What expected KPI event and score status is recorded at each boundary? Which events require a disciplinary decision rather than another KPI calculation?
- The employee has late events on 30 June and 1 July, then receives a salary change and an unrelated ordinary-pay policy amendment. Which events belong to each KPI assessment? Salary/ordinary amendments must not be mistaken for already-confirmed ordinary late-count resets.
- A monthly site schedule contains approved sick leave, a paid holiday, work travel and completed OT. Which intervals count toward committed-hours shortage and KPI? Specify the approved expected classification without choosing a shortage formula here.
- An emergency half-day has no attachment at submission, but evidence arrives before review or after the month is locked. Does any KPI event arise, on which date, who decides it, and how is an accepted remediation represented? Current code blocks approval; it does not award a one-percent penalty.

### D02. Paired-Worker Waiting Loss

SRS: OFF-005, SITE-005, MD-010; XML paragraphs 571-572, 597-598, 1145-1146. Code: C06, C04.

| Dimension | Exact unresolved decision |
|---|---|
| Formula/base | Is an approved loss a time deduction, money, KPI event or combination? Whose financial/performance base applies, on what effective date, with which divisor/day unit and rounding? Define the relationship between the delayed employee's own lateness and the other worker's verified wait. |
| Attribution/cap | Who bears the effect: delaying employee, waiting employee or an explicitly split allocation? Are overlapping waits counted once, per worker or per task? What per-event/day/period maximum applies? The existing ordinary/late larger-only rule does not decide three-way overlap with waiting loss. |
| Reset/period | Define the accounting window, policy-change boundary and treatment of a report verified in a later month. Source months stay locked; routing uses the confirmed first later unlocked target. |
| Eligibility | What proves an effective work pair/task dependency? Can workers at different sites or with different coordinated starts qualify? Exclude/define waits during paid leave, breaks, alternate productive work, client access delays and approved later reporting. |
| Authority/evidence | Identify pair-assignment owner, evidence submitter, independent verifier and effect decision permission/scope. Specify required timestamp/task records, source reports/punches, witness/evidence reference, disputes and supersession. HR correction/OT authority is not blanket authority for this new loss action. |
| Settlement | Only an approved, legally authorized financial interpretation may produce a proposed line. Exact source and decision identities must survive into the Finance receipt; factual wait verification alone cannot be posted as a charge. |

Concrete questions:

- Worker A is ready at 10:00; B reports at 10:45; A documents task-specific waiting until 10:20 and performs other work afterward. Which interval is attributable, whose effect is proposed, and how does it interact with B's ordinary shortfall/late candidate?
- A and C both wait for B during overlapping intervals. What is the maximum attributable duration and how is one delayed task distinguished from two separately verified losses?
- HR has directly moved the site's start to 11:00. B arrives at 10:45 and A claims waiting from 10:00. What pair instruction could establish an eligible interval? Do not infer lateness from the old start.

Model limitation: `WaitingTimeEvidence` requires a verified actor, policy and approval reference at construction. Its string `WorkPairReference` and duration constraints do not establish pairing/causation, approved authority or deduplication. Do not use this as an unreviewed intake record with a fabricated verifier/approval.

### D03. Six Extra Leave Days or Equivalent Pay at 100% KPI

SRS: LEAVE-011, MD-011; XML paragraphs 656-657, 1147-1148. Code: C07-C09.

| Dimension | Exact unresolved decision |
|---|---|
| Formula/base | For cash election, specify the approved compensation base, effective salary date, day divisor, six-day equivalence, fractional units and rounding. The confirmed ordinary salary and OT formulas do not specify this benefit's valuation. |
| Reset/eligibility | Which finalized KPI revision and assessment period prove 100%? Is 100% tested before or after approved effects, and is display rounding relevant? Which staff/service categories qualify, at what date, with what treatment of partial years, termination and a later score correction? |
| Grant/election | Management "may grant" is discretionary. Who chooses days versus pay, can the election be split, and is there one grant per employee/year? Is the six-day benefit separate from the regular 18-day pool and sick/casual limits? When can it be used; can it expire, carry or be encashed? |
| Authority/evidence | Specify the new grant/election action and permissioned authority. Ordinary direct Admin leave review remains confirmed but does not prove a discretionary bonus was granted. Retain the finalized KPI source, management decision, policy, award year and election revision. |
| Settlement | Define when the days become available and how a later correction reverses unused/used benefit. Cash requires approved financial lines and exact Accounts/Finance posting receipts. Prevent receiving both the full day award and full cash equivalent for one grant. |

Concrete questions:

- At fiscal close, a retained score displays 100%, while its precise source value is 99.995%. Is the employee eligible, or must the assessed value itself equal 100%? What approved precision governs the comparison?
- A qualifying employee has already used all regular 18 days, asks for six additional days, then chooses cash before those days are used. What grant/election replaces what, and which entitlement/consumption records change?
- A score correction arrives after three awarded days have been used or a cash line has been posted. What approved adjustment applies? Historical leave, locked months and posting evidence cannot be erased.

Implementation gap: `leave-settings-v1` rejects changing annual days to 24, and quota reads ignore Entitlement/Adjustment entries. This requires a deliberate new supported benefit contract and balance interpretation after decisions; neither an arbitrary ledger row nor relabeling ordinary leave completes it.

### D04. Unused 18-Day Leave Encashment at >=70% KPI

SRS: LEAVE-012; XML paragraphs 658-659 and 668. Code: C09-C10.

| Dimension | Exact unresolved decision |
|---|---|
| Formula/base | Specify encashment salary/wage basis, effective date, day divisor, fractional unused-day/minute conversion, currency and rounding. Do not infer BASIC/month-days from ordinary absence or nine hours from OT. |
| Eligible balance | Does "unused 18-day leave" include both sick and casual unused balances, other regular-balance leave kinds, and partial days? Does it exclude the six-day benefit and injury rest? Define caps, pending requests/reservations, cancellations, prior encashment and carried balances. |
| Reset/eligibility | Confirm the claim/cutoff window within the established July-June leave year, service/category rules, partial years and leaving employment. Which finalized KPI revision/date proves >=70%, and what happens after a score amendment? The 70% threshold itself is not questioned. |
| Authority/evidence | Which specific management decision approves encashment, with what permission/scope and separation from requester/beneficiary? Bind score, exact fiscal usage/reversals, leave policies/day denominators, compensation evidence and proposed financial lines. Ordinary leave approval is not an encashment approval. |
| Settlement | At which explicit lifecycle transition is the eligible balance reserved/consumed: approved claim, locked handoff or posting receipt? Define releases, rejection/withdrawal, retries, partial posting and reversal handling. Receipt confirmation is necessary for posted status; handoff alone never suffices. |

Concrete questions:

- At 30 June an employee has used 10 regular days, with one pending two-day application and a separate six-day benefit. Which exact balance is eligible to encash, and how is simultaneous leave approval prevented from consuming the same days?
- A score is 69.99%, 70.00% or 70.01%. Specify the authoritative comparison precision and required finalized-score evidence; no derived score is supplied here.
- A claim covers 1.5 unused days under retained eight-hour and nine-hour leave settings. Which exact day units and salary date are approved for valuation? Existing usage preserves each historical policy denominator.
- An approved encashment is handed off but has no Finance posting receipt. What balance state is retained? What happens if the receipt covers only part of the approved lines? Do not mark the whole claim posted.

Model limitation: `LeaveEncashmentRequest` accepts caller-supplied assessed percent, minutes and proposed amount; it validates ranges and a pending submission reference, not >=70% eligibility, unused balance or a trusted calculation. Its existence is not an encashment engine.

### D05. Long-Leave Recovery, Pre-Leave and Post-Return

SRS: LEAVE-013, related SITE-009/010 and MD-006; XML paragraphs 605-608, 660-661, 1137-1138. Code: C11, C18-C19.

| Dimension | Exact unresolved decision |
|---|---|
| Formula/base | What creates the recoverable obligation: which leave kinds, duration definition of "long", pending hours or committed-hours shortage? State exact required minutes and qualifying credit rules, rather than making every long absence recoverable. Any money consequence needs its own approved base/date/divisor/rounding and legal evidence. |
| Eligibility/credit | Which site staff qualify? Can ordinary worked hours, approved extra duty, OT, travel, holiday/off-day work or make-up hours discharge it? Define allocation when one interval could satisfy regular duty, OT and two recovery commitments; avoid choosing a double-credit rule without approval. |
| Reset/deadline | What pre-leave window and post-return deadline apply? How are partial fulfillment, cancellations, extended leave, transfer, termination and fiscal boundary handled? Define priority across multiple commitments and effect of a later attendance correction. |
| Authority/evidence | Specify the commitment/fulfillment/extension/waiver decision permissions and authority. Bind approved leave, original obligation, exact qualifying session/decision revisions, policy and separate fulfillment allocations. A commitment is a promise; it is not proof of completed work. |
| Settlement | Is fulfillment performance/time-only or does an approved financial debit/credit arise? Define missed-deadline treatment, waived balance and any receipt-bound adjustments. Do not automatically withhold salary or deduct the commitment's unfulfilled minutes. |

Concrete questions:

- A site employee requests ten leave days and proposes six recovery hours before leave plus four after return. What approved rule establishes the obligation and accepts those commitments? The proposed hours are illustrative, not a debt formula.
- Two worked hours after return already have an approved OT award and coincide with two outstanding recovery commitments. Which exact interval allocation is allowed, and is any existing OT award adjusted through a separately approved action?
- Return is delayed or employment ends before the agreed deadline. Who may extend/waive the commitment, and what supported outcome is shown instead of an automatic salary penalty?
- A prior day's recovery evidence is corrected after its month was locked. Route any approved adjustment to the first later unlocked month; define its financial interpretation without reopening the source.

Related unresolved extended-duty rule: factual overnight sessions and the confirmed OT implementation do not implement the SRS ten-hour reset / next-day 4.5-hour / twelve-extra-hour rest or absence treatment. Require an approved timeline example with dated schedule, duty, rest, next-day classification and recovery/OT allocation before introducing those effects. Do not reopen the confirmed OT rate.

## 5. Financial Month Readiness and Settlement

### D06. Missing Pre-Lock Financial Consumer

Current executable gaps are specific:

1. C03 returns `PayrollReady=false` for both calculated and unavailable daily time results. C12 checks that value and adds `attendance_financial_calculation_required` even when the time policy is calculated.
2. C04 may produce/save a reviewable monthly deduction input set, but its response always sets `PayrollReady=false` and `PaymentInstruction=false`. Its `ready` state means candidate calculation completeness; `CanSave` does not require formally approved legal status. There is no path from saved input revisions to month-lock financial membership.
3. C13 emits `pending_overtime` for pending requests and `overtime_payroll_settlement_unavailable` for approved requests. The name of the second issue must not be read as requiring a Finance posting receipt before the Attendance lock: what is missing here is the verified pre-lock consumer of exact approved awards.
4. C12 emits `carry_forward_financial_calculation_unavailable` whenever target carry-forward entries exist. C18 exposes their factual state as `pending_policy`. Its `posted_adjustments_only` label means applied factual ledger entries, not posted financial lines.
5. C12 validates exact coverage, current saved summaries, protected evidence and immutable session membership. The new KPI/waiting/benefit/encashment/recovery models are not enumerated by this readiness path. Their absence today does not establish verified zero impact or approved non-applicability.
6. C19 returns `site_not_assigned` without a historical site. Schedule-dependent office policy/leave/time cannot be made available by silently supplying a default office shift.

Safe dependency sequence: verify supported sources and financial calculation evidence; finalize Attendance with exact immutable source membership; prepare the post-lock handoff; validate explicit Accounts/Finance posting receipts. C14 requires an existing `AttendancePeriodLockEventId` on a handoff line, so requiring that line or its posting before creating the same Attendance lock would be circular.

Remaining contract questions, without new management rate decisions:

- What versioned protected pre-lock manifest will bind each saved deduction revision and exact line set, approved OT award, applicable adjustment/effect, jurisdiction/legal decision and source digest? Define producer completeness and stale-source detection, not a blanket `PayrollReady=true` switch.
- For each policy-required extension, what approved applicability evidence permits `not_applicable`? Missing producer, unsupported schema, incomplete history, pending decision and conflicted evidence must remain distinguishable. Do not make every absent model a universal blocker irrespective of policy applicability, or treat an empty table as approved exemption.
- What existing finance boundary will consume the lock manifest and report source inclusion/rejection? Exact permission identifiers, scope mapping, bounded payload and transaction/replay behavior need an implementation contract; Accounts/Finance posting authority itself is already confirmed.
- What formal approval record activates a legal rule for the applicable employee/jurisdiction/date, and what evidence proves the proposed authority was approved? Configured location and HR-published rates alone cannot close this gap. No statutory conclusion is made by this audit.

### D07. Financial Carry-Forward and Cross-Month Reconciliation

Code: C04, C18. Current ordinary/late overlap already follows larger-only handling. C04 emits `prior_deduction_reconciliation_required` when a threshold compares ordinary charge candidates from an earlier month. The engine recomputes prior candidates; it does not read proof of what was actually handed off or posted.

Remaining decisions/contracts:

- For a corrected locked source, which original policy/compensation evidence prices the revised interpretation, and which prior exact financial revision/lines and posting receipts are the baseline? Specify valuation date and any rounding/reversal treatment for a new adjustment component; do not guess from a factual minute delta or current salary.
- How are prepared-but-unposted, handed-off-but-unposted, partially posted, fully posted and reversed earlier lines reconciled? The confirmed settlement rule fixes what proves posted status; it does not specify accounting allocation across these cases.
- How is approved OT reconciliation represented when a later factual correction changes its source? Preserve the existing award and confirmed formula; no silent re-award or rewrite.
- Define source identity across revision chains and batches, exact supersession/reversal links, target revalidation and prevention of paying/charging an original and all its revisions. First later unlocked routing is already confirmed.

Concrete examples/questions:

- A late group includes 29 September, 30 September and 2 October, and the saved September ordinary lines have no posting receipt. Which retained line set is used for reconciliation? A prior calculated candidate is not evidence of a prior posted deduction.
- The same group instead has a receipt covering the exact September lines, followed by a later reversal receipt. Which net posted baseline and trace references govern the October input? No amount is guessed here.
- A correction changes a locked September duty interval; October and November are locked. December is the first later unlocked target. What original financial evidence and exact approved adjustment lines are retained there? A December salary change must not implicitly decide source valuation.

### D08. Receipt Contract and Model Limitations

Confirmed requirement: only explicit Accounts/Finance-permissioned receipts tied to exact revision, lines, amounts and posting can establish settlement. No additional salary-release capability is in scope.

C15's `PayrollHandoffProcessingRecord` retains only batch ID, external reference, protected result reference, actor and time. It has no first-class exact line/revision/amount/posting fields, and there is no Application service validating such contents inside the protected result. A processing-reference string is therefore not implemented settlement evidence. C14's lock method and FKs also do not prove the correct approved workflow by themselves.

An implementable receipt contract must bind and validate:

- Exact locked handoff revision/content digest, lock-event identity, source revision and immutable line IDs/keys; amounts, currency and debit/credit meaning must match the approved lines.
- Explicit current Accounts/Finance permission and source scopes, actor/time and authoritative posting reference/status/date. Role names alone, a transport acknowledgement, a generated export or a receipt upload do not establish posting.
- Protected receipt evidence and its integrity, external system/source identity, exact retry identity, conflicting retries and durable atomic audit. Client-provided `posted=true` is not verified posting evidence.
- Each covered line and any partial/rejected/uncovered line, linked reversal/correction evidence and duplicate detection across batches. C14's unique source tuple includes batch ID; it prevents duplicates within that batch, not reuse of one source in another batch. Unique trace IDs do not independently prevent new traces for the same source.

Still needed: choose/identify the authoritative receipt source and payload; specify acceptance of manual attestation versus an authenticated finance adapter; map confirmed Accounts/Finance authority to actual permission identifiers; define partial and reversal semantics and source-consumption transitions. These are missing implementation/evidence contracts, not a request to reapprove the confirmed settlement rule.

Concrete examples/questions:

- Receipt R names batch revision 2, but the frozen manifest is revision 3 or one line amount differs. Reject settlement for the mismatched set; which exact conflict/evidence response is required?
- R covers two of three locked lines. Those covered lines can only be marked posted after verification; the remaining line and whole-claim completion need the explicit partial-settlement contract. Do not infer a full batch posting.
- The same approved award is proposed in a second batch with a new trace ID. What cross-batch source-consumption identity prevents duplication? A linked approved reversal/adjustment must remain distinguishable from a repeated award.

## 6. Relevant Reports: Existing Outputs and Missing Projections

| Report | What is actually available | Concrete extension and dependency |
|---|---|---|
| Daily/monthly attendance | Bounded factual employee/date register/dashboard; time/classification separately available on day reads. Factual report columns are explicitly not payroll (C03, C16) | A policy register can project verified classifications/source revisions and unknown states. Payroll-ready matrix requires D06; absence cannot be inferred from missing accepted sessions. |
| Late and KPI | Ordinary deduction lines retain threshold source dates, policy/compensation fingerprints, candidates and suppression. No annual KPI report | Separate ordinary late explanation from finalized KPI year/score/event/disciplinary status. D01 supplies KPI meaning; a factual late-event list must not report an invented score. |
| Paired-worker waiting | Model only; existing site reports and punch/session audit can be evidence inputs | Waiting evidence/attribution/decision register after D02 authority and provenance exist; preserve both employee scopes and avoid exposing another worker's salary/medical evidence. No payable waiting amount before valuation approval. |
| OT | Own/scoped requests, HR decisions and separately permissioned retained financial detail exist; not a complete site/job financial export | Project requested/approved/rejected durations, exact approved award and inclusion/posting status. Consume existing award evidence (C18); do not recompute it or call it paid. |
| Ordinary leave | Own/scoped request/history, verified balance, evidence-presence status, decisions/cancellations and self preview exist (C07-C09) | Utilization/evidence report can reuse current semantics. Extra-day election, encashment eligibility and recovery status need D03-D05; model percent/minutes alone cannot label an employee eligible. |
| Benefit/encashment/recovery | No operational report producer | Award year, KPI revision, entitlement/election, exact balance units, claim/commitment decision, fulfillment allocation and Finance line/receipt/reversal references. Report unavailable rather than zero benefit/debt when evidence is missing. |
| Payroll handoff/reconciliation | No working handoff/report service; financial deduction inputs are reviewable separately (C04, C14-C15) | Report prepared revision, locked manifest, approved source lines, handoff state, exact posted receipts and unreconciled differences distinctly. D06-D08 are dependencies; handoff alone must never display posted/paid. |
| Punch/session and business audit | Management raw punch/session-revision read APIs exist with historical source scopes; audit is persisted by implemented workflows (C17) | Reuse original evidence and decisions. Do not sum session revisions as separate work or expose templates, private paths, salary, medical attachments or GPS through general factual export permissions. |

CSV/XLSX/PDF renderers and durable jobs are implemented for the factual report, not arbitrary financial or HR reports. Existing range is 1-25 explicitly selected employees and at most 31 inclusive dates (maximum 775 cells). `AttendanceJobs.Enabled` is false by default; requests return `attendance_export_worker_not_enabled` while disabled (C16-C17). That is an unavailable runtime export path, not a missing renderer.

Financial projections must use their existing separate financial permissions and current/original historical scopes. `attendance.reports.read/export` must not acquire salary access merely because an export format exists. New ordinary salary self-service permissions/columns are not supplied by the management payroll-input API. Preserve generation time, filters, policy period, data/revision kind, source trace and lock state; unknown or pending states remain explicit.

## 7. Work That Can Proceed Independently Without Invented Policy

These are future implementation packages, not changes performed by this audit. Standalone projections/validators can be prepared and reviewed in owned files without shared DI, DbContext, permissions, schema, SQL or Git edits. Making them reachable or persistent later needs coordinated integration. No package below authorizes runtime worker enablement or real-account acceptance.

| Package | Safe concrete deliverable | Boundary / completion evidence |
|---|---|---|
| S1: Existing-source report projections | Bounded read-only projections of ordinary leave utilization/cancellation/evidence status, existing financial input revision/line explanations, and retained OT request/award detail. Use each source service's established permission and provenance; keep financial output separate | Project only actual source values. Show prepared/pending/unavailable where appropriate. No new KPI, waiting charge, benefit, encashment amount, recovery debt or payment claim. Review denied/historical-scope and stale-source behavior before integration. |
| S2: Readiness explanation | Sidecar diagnostic projection of current month-lock issues plus named missing consumer capabilities; identify employee/date/source and distinguish factual calculation, prepared financial inputs, lock, handoff and posted receipt | Preserve all existing blockers and failure propagation. Do not set `PayrollReady=true`, make posting a pre-lock requirement, invent applicability, or invent a new emitted API code without integration. |
| S3: Exact manifest/receipt validators | Pure validators over explicit supplied immutable manifests and receipt evidence: revision/line/currency/amount binding, duplicate source/receipt identity, missing evidence and conflict detection | Uses already-calculated amounts only; does not price any extension. Treat untrusted posting/authority as unverified. Persistence, permission mapping, partial/reversal semantics and authoritative receipt adapter remain D06-D08 integration work. |
| S4: Trace and revision consistency | Read-only reconciliation inventory: identify earlier deduction revision/line references, ordinary late groups crossing months, factual carry-forward chains and existing OT awards awaiting an actual consumer | Detect missing/conflicting evidence without settling it. A source-candidate comparison is not posted history. Financial delta/consumption decisions remain D07-D08. |
| S5: Decision/acceptance fixtures | Synthetic expected-input cases using D01-D08 boundaries, with policy-dependent expected results explicitly awaiting approval; later focused checks can use the existing standalone harness style | No new test project, build, SQL fixture or account creation in this audit. Do not encode unresolved outputs as passing assertions. |

Blocked until policy is approved: KPI score computation/reset; waiting attribution or charge; extra-day grant/cash valuation; encashment eligibility/valuation/consumption; recovery obligation/credit/penalty; extended-duty rest-credit rules; legal activation; financial carry-forward valuation. These cannot be completed safely by adding generic configurable fields, treating absent producers as zero, or fabricating approval references.

Checklist for the eventual coordinated implementation:

- [ ] Close D01-D05 with explicit applicability, formula/base, period/reset, eligibility, authority, evidence, interaction and settlement decisions plus approved boundary examples.
- [ ] Supply formal legal authority/applicability approval evidence; keep the present proposal unapproved until that evidence exists.
- [ ] Agree D06-D08 protected manifests, source inclusion, receipt authority mapping and partial/reversal/reconciliation lifecycle.
- [ ] Version new contracts and preserve existing schema-1/2 canonical history, summary fingerprints, exact leave units, source locks and retained OT award evidence.
- [ ] Establish one authoritative consumption identity across source revisions/batches; distinguish replacement input sets from incremental posted adjustments.
- [ ] Revalidate preview fingerprints, source revisions, scopes, legal evidence and target lock state before atomic writes. Exact retry must recheck current authority.
- [ ] Keep general day/factual DTOs free of financial/medical/private evidence. Financial report and posting access remain explicit.
- [ ] Obtain separately scoped operational acceptance for pending migrations, authenticated SQL concurrency/rollback, protected storage/restart, genuine provider/device behavior and worker/export activation. Historical offline/anonymous checks do not establish these.

## 8. Source Index

Line references identify the audited local code; later concurrent edits may move them. Links are relative to this document so the sidecar remains usable within the repository.

| Ref | Exact evidence location |
|---|---|
| C01 | [Application registrations](../../src/Cosmix.Application/DependencyInjection.cs), lines 64-68: real daily calculator, financial producer/service and jurisdiction reader. [Infrastructure registrations](../../src/Cosmix.Infrastructure/DependencyInjection.cs), line 71: financial revision ledger. |
| C02 | [AttendancePolicyRules.cs](../../src/Cosmix.Domain/HR/Policies/AttendancePolicyRules.cs), lines 10, 37, 59 and 118: schema fields and schema-3 validator; no KPI/waiting/benefit/recovery section. |
| C03 | [ConfiguredAttendancePolicyCalculator.cs](../../src/Cosmix.Application/Attendance/Calculation/ConfiguredAttendancePolicyCalculator.cs), lines 10-33: time-only calculated/unavailable results, both financially unready. [AttendanceCalculationSnapshotProducer.cs](../../src/Cosmix.Application/Attendance/Calculation/AttendanceCalculationSnapshotProducer.cs), dependency checks following line 30: actual policy, leave, schedule, employment and duty validation. |
| C04 | [AttendancePayrollInputService.cs](../../src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollInputService.cs), lines 25-57 and 140-153: immutable saves and explicit non-ready/non-payment/legal-review statuses. [AttendancePayrollInputRules.cs](../../src/Cosmix.Domain/Attendance/PayrollInputs/AttendancePayrollInputRules.cs), lines 37-109: ordinary/late candidate calculation and prior-month blocker. [AttendancePayrollInputProducer.cs](../../src/Cosmix.Application/Attendance/PayrollInputs/AttendancePayrollInputProducer.cs), lines 35-87: actual-source manifest. |
| C05 | [PolicyImpactAssessment.cs](../../src/Cosmix.Domain/HR/PolicyEffects/PolicyImpactAssessment.cs), lines 9-29: proposed values, policy/calculation/approval references. [PolicyEffects/Types.cs](../../src/Cosmix.Domain/HR/PolicyEffects/Types.cs), line 3: effect kinds, not executable rules. |
| C06 | [WaitingTimeEvidence.cs](../../src/Cosmix.Domain/HR/PolicyEffects/WaitingTimeEvidence.cs), lines 9-30: distinct workers, exact interval, verified minutes and required authority references. [WaitingTimeEvidenceConfiguration.cs](../../src/Cosmix.Infrastructure/Database/Configurations/HR/WaitingTimeEvidenceConfiguration.cs), lines 11-27: duration/identity constraints and FKs, without a business workflow. |
| C07 | [LeavePolicySettings.cs](../../src/Cosmix.Application/HR/Leaves/LeavePolicySettings.cs), lines 11-29 and 43-44: explicit quotas/day unit/categories and July-June year. [LeaveService.cs](../../src/Cosmix.Application/HR/Leaves/LeaveService.cs), approval/utilization at line 164 and cancellation handling at line 221. |
| C08 | [LeaveTreatmentRules.cs](../../src/Cosmix.Application/HR/Leaves/LeaveTreatmentRules.cs), lines 9-43: evidence/service/quota checks. [LeaveReviewAuthority.cs](../../src/Cosmix.Application/HR/Leaves/LeaveReviewAuthority.cs), lines 9-23: current fixed Admin/SuperAdmin assignment and explicit review permission. |
| C09 | [LeaveRepository.cs](../../src/Cosmix.Infrastructure/Repositories/HR/LeaveRepository.cs), lines 72-99 and 140-144: usage validation, historical denominators, Utilization/Release-only quota query. [LeaveLedgerEntry.cs](../../src/Cosmix.Domain/HR/Leaves/LeaveLedgerEntry.cs), line 12; [Leaves/Types.cs](../../src/Cosmix.Domain/HR/Leaves/Types.cs), line 3: generic ledger model/kinds. |
| C10 | [LeaveEncashmentRequest.cs](../../src/Cosmix.Domain/HR/Leaves/LeaveEncashmentRequest.cs), line 11: proposed percent/amount constructor; `Submit` method binds a pending request, not final business eligibility or payment. [LeaveEncashmentRequestConfiguration.cs](../../src/Cosmix.Infrastructure/Database/Configurations/HR/LeaveEncashmentRequestConfiguration.cs): model constraints. |
| C11 | [LeaveRecoveryCommitment.cs](../../src/Cosmix.Domain/HR/Leaves/LeaveRecoveryCommitment.cs), lines 8-22: commitment model only. [LeaveRecoveryCommitmentConfiguration.cs](../../src/Cosmix.Infrastructure/Database/Configurations/HR/LeaveRecoveryCommitmentConfiguration.cs), lines 11-21: positive minutes and FKs. |
| C12 | [MonthLockService.cs](../../src/Cosmix.Application/Attendance/MonthLocks/MonthLockService.cs), lines 33-59, 75-122: revalidation/lock creation, carry-forward blocker, daily financial flag and protected membership validation. |
| C13 | [MonthLockRepository.cs](../../src/Cosmix.Infrastructure/Repositories/Attendance/MonthLockRepository.cs), lines 112-127: source checks and pending/approved OT issues. |
| C14 | [PayrollHandoffBatch.cs](../../src/Cosmix.Domain/HR/PayrollHandoff/PayrollHandoffBatch.cs), lines 39-51: revision/lock model. [PayrollHandoffLine.cs](../../src/Cosmix.Domain/HR/PayrollHandoff/PayrollHandoffLine.cs), lines 14-29: lock/source/value traces. [PayrollHandoffLineConfiguration.cs](../../src/Cosmix.Infrastructure/Database/Configurations/HR/PayrollHandoffLineConfiguration.cs), lines 27-29: lock FK, batch-scoped source uniqueness and trace uniqueness. |
| C15 | [PayrollHandoffProcessingRecord.cs](../../src/Cosmix.Domain/HR/PayrollHandoff/PayrollHandoffProcessingRecord.cs), lines 10-25: batch/reference/protected-result model. [PayrollHandoffProcessingRecordConfiguration.cs](../../src/Cosmix.Infrastructure/Database/Configurations/HR/PayrollHandoffProcessingRecordConfiguration.cs), line 17: batch/external-reference uniqueness. [Application PayrollHandoff README](../../src/Cosmix.Application/HR/PayrollHandoff/README.md): folder is foundation only; file inventory has no service. |
| C16 | [AttendanceReportContracts.cs](../../src/Cosmix.Application/Attendance/Reports/AttendanceReportContracts.cs), lines 11-62: factual DTO/permission/metadata boundaries. [FactualAttendanceDocument.cs](../../src/Cosmix.Application/Attendance/Reports/FactualAttendanceDocument.cs), lines 10-15: factual generator/columns. |
| C17 | [AttendanceReportService.cs](../../src/Cosmix.Application/Attendance/Reports/AttendanceReportService.cs), lines 54-60 and 206: worker gate and range validation. [AttendanceReportsController.cs](../../src/Cosmix.Api/Controllers/Attendance/AttendanceReportsController.cs), lines 18-52: scoped factual reads and real export contracts. [AttendanceAuditController.cs](../../src/Cosmix.Api/Controllers/Attendance/AttendanceAuditController.cs), lines 16-23: scoped raw/session revision audit. |
| C18 | [CarryForwardRules.cs](../../src/Cosmix.Application/Attendance/CarryForward/CarryForwardRules.cs), line 7: first available later month. [CarryForwardContracts.cs](../../src/Cosmix.Application/Attendance/CarryForward/CarryForwardContracts.cs), lines 26, 49 and 52: factual pending-policy state. [OvertimeFormula.cs](../../src/Cosmix.Application/Attendance/Overtime/OvertimeFormula.cs), lines 9-24 and 31-63: confirmed rate/exact duration. [OvertimeEligibilityService.cs](../../src/Cosmix.Application/Attendance/Overtime/OvertimeEligibilityService.cs), lines 20 and 34: office ineligible. |
| C19 | [SiteRosterResolver.cs](../../src/Cosmix.Application/Attendance/Rosters/SiteRosterResolver.cs), lines 5-18: site-only runtime resolver and explicit missing-site result. |

Primary requirement source: [HR_Attendance_Employee_Mobile_SRS_v1.2.docx](../requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx), sections 8, 9, 15 and 19. Section 15 XML anchors: 975-1000 for report content and 1007-1014 for export controls.

Current implementation/evidence records: [Policy-Leave-Implementation.md](Policy-Leave-Implementation.md), [Leaves.md](Leaves.md), [Payroll-Inputs-Implementation.md](Payroll-Inputs-Implementation.md), [Payroll-Inputs.md](../attendance-policies/Payroll-Inputs.md), [Company-Settings.md](Company-Settings.md), [MonthLocks.md](MonthLocks.md), [Reports.md](notifications/Reports.md), [Verification.md](Verification.md). Older continuation/proposal statements are historical where these latest records and actual code supersede them.
