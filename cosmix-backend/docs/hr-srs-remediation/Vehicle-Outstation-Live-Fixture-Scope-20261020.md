# Vehicle/outstation synthetic acceptance approval scope

20 October 2026. **Proposal only; no provisioning or execution performed.**
Parent owns approval, authority changes, fixture preparation and execution.
No migrations, real face/OTP, legal activation, Accounts posting or payment.
This worker used source/document reads only: no SQL, HTTP, host, credentials or
Git operations. Only this new document was written.

## Evidence and exact blockers

Latest baseline: [six-gap continuation](HR-Six-Gap-Continuation-20261020.md).
Parent's fresh SQL findings, not independently queried here: Policies,
PolicyVersions, EmployeeAssignments, trips, mileage, service records, fuel,
deployments and claims are all zero. The one vehicle and one returned custody
belong to V1 and **must not be reused or changed**.

Parent confirms existing HR/Admin/SuperAdmin have All-scoped vehicle
self/manage/commute/custody/trips/fuel/maintenance/incident grants. Missing catalog
entries/grants are `attendance.deployments.read`, `.request`, `.decide` and
`attendance.vehicle.policy.manage`; this is not a missing-table diagnosis.

`AccessManagementService.GrantRolePermissionsAsync` calls `GetOrAddPermissionAsync`
for a supported permission on a new, never-assigned custom role. No migration
is needed. However, fixed roles and already-assigned roles are immutable.
`AttendanceOperation.MatchingGrantsAsync` requires the business permission on
the **same fixed-role assignment** when `requireSystemRole:true`. Every vehicle
policy route and deployment/home-break/claim decisions use that restriction;
deployment decisions additionally require actor role `ADMIN`/`SUPERADMIN`.
A separate custom grant on a genuine Admin currently fails.

**Recommendation for parent:** implement/review the explicitly scoped genuine
active fixed Admin plus delegated business permission design, preserving current
MFA/session/grant revalidation, scope, independent review and audit authority.
Do not change fixed roles or weaken shared authorization. Existing benefit
delegation does not automatically authorize these modules. Vehicle policy
publication remains blocked pending its own approved module change; no current
or future publication is proposed in this packet.

## First executable batch: one outstation deployment and claim

Choose this first, **after** the deployment authority fix and explicit source
seed approval. It cannot run immediately with zero employment assignments.
Use the three existing synthetic identities: H = HR beneficiary/requester,
A = fixed Admin reviewer, S = fixed SuperAdmin access provisioner. No accounts,
credentials, employees or fixed-role assignments are created. H cannot approve
H's deployment/claim. One independent decision per request is sufficient.

| Exact additions | Count and test-only values |
| --- | --- |
| Source master test seeds | 5: department `TEST-VO-D`, designation `TEST-VO-J`, staff category `TEST-VO-C`, employment type `TEST-VO-E`, branch `TEST-VO-B`; names `Synthetic VO <kind>`, active; branch city/cityId null. These are fixture masters, not company policy or taxonomy acceptance. |
| Employment test seed | 1 assignment for H, referencing those five IDs; 2026-09-01 inclusive to 2026-10-21 exclusive; SiteId and ReportingManagerId null. No employee lifecycle/status or hiring approval is fabricated. |
| Allowance policy test seeds | 1 definition `TEST-VO-ALLOW`, 1 `allowance.v2` version and 1 Employee-target assignment to H, all effective 2026-10-18 inclusive to 2026-10-20 exclusive. Explicit testseed provenance; not API publication evidence. |
| Access provisioning | 2 new custom roles: `TEST-VO-REQUEST` with deployments.read/request assigned to H; `TEST-VO-REVIEW` with deployments.read/decide assigned to A. Exactly 3 catalog entries, 4 role-permission links, 2 role assignments and 2 All-scope rows. Effective from actual provisioning time through 2026-10-21T19:00:00Z; pin actual start in execution manifest. |
| API business writes | 1 deployment, 1 OutstationDaily allowance claim, 1 claim submission, 2 approval headers and 2 independent decisions. Zero home breaks, meal claims, settlements and payroll rows. |

Source seed total: **9 rows** (five masters, employment, policy, version,
assignment), separate from access and API-generated rows. This deliberately
enumerates new synthetic masters instead of assuming unknown existing FK targets.
An existing-master reuse alternative would require a revised manifest/count.
S provisions through existing access services using current fixed-assignment
All grants for `access.roles.read`, `access.roles.create`,
`access.roles.permissions.manage`, `access.accounts.read`, `access.accounts.assign`.
Assigning to protected Admin A requires SuperAdmin S; no self-assignment.
Grant before assignment, retain rowversions, then reauthenticate affected users.

Deployment values: destination `TEST-VO-DESTINATION`, category `TEST_VO`;
2026-10-18T04:00:00Z to 2026-10-19T12:00:00Z (Pakistan 09:00 to next-day 17:00);
payable date 2026-10-19. Reason/evidence text starts
`TEST-VO-20261020 synthetic only`. Evidence text is not an uploaded document.

Explicit synthetic valuation: PKR 100.00/day, `LocalCalendarDays`;
DepartureDay/ReturnDay/SameDay/InterruptedDay = `FullDay`;
ElapsedRemainder = `NotApplicable`; rounding 2 digits, `ToEven`, `FinalClaim`;
PolicyTransition = `LocalCalendarDayEffectivePolicy`; eligible categories
`["TEST_VO"]`. Expected **2 days, PKR 200.00**, not a 40-hour rule.
Required schema-2 companion fields: OfficeDutyStartLocalTime `09:00`,
ContinuityGapToleranceMinutes 0; OfficeMeal/SiteDinner/SiteBreakfast/SiteLunch
each RequiredContinuousDutyHours 8 and Amount 0.00; OutstationDaily hours null,
Amount 100.00; WorkTravel null. No meal workflow is exercised.

Run existing `DeploymentService` request -> A decision -> calculation -> H claim
-> A claim decision -> retained evidence. Use exact returned deployment/approval
versions and calculation fingerprint. Test same-key retries before/after approval,
changed-payload and new-key duplicate denial, and self-review denial; no extra
successful business rows. Approval is HR acceptance only.
If delegation is not implemented, stop at permitted request/read/preview and
explicit decision denial; do not call that a completed claim batch.

Employment is read for every Pakistan date and must remain the same assignment.
Claim authorization also resolves payable-date scopes. Site/team/project scopes
come from actual historical membership; this fixture has a branch and no site.
No site, roster, holiday, attendance punch, work session, face or OTP fixture is
required by this deployment calculation. Existing overlapping Pending/Approved
deployments or meal claims must remain absent. Zero source rows are not employment.
Site reassignment requires an existing open assignment; hiring materialization
is a broader workflow. Neither is a shortcut to the missing initial row here.

## Vehicle additions: separate, gated continuation

Vehicle policy resolution is **database-wide by date**, not employee/site assigned.
Staff eligibility restricts use but does not isolate policy selection. Parent must
approve the affected date windows and prevent unrelated vehicle work during the
run. Even retained V1 calculations could encounter the new policy selection.

Shared vehicle fixture configuration: **1 new policy definition
`TEST-VO-VEH`, 3 explicitly labelled testseed versions**:
P1 [2026-09-01, 2026-09-16), price 100 PKR/litre;
P2 [2026-09-16, 2026-10-20), price 200;
P3 [2026-10-20, 2026-10-21), price 200.
All use 20 km/litre, PKR, H-only FieldVisits eligibility, Odometer other-work
basis, tuning/overhaul thresholds 50 km, `EveryThreshold`, fixed PKR 50/75
respectively, Motorcycle only, workshop `TEST-VO-WORKSHOP`.
Each version's staff/workshop dates equal its own period; authority/evidence
strings start `TEST-VO-20261020`. Configure explicit daily attribution,
`OncePerTripDay`, `PerSegmentTwoDecimalsToEven`, reviewer roles
`["ADMIN","SUPERADMIN"]`; monthly/incident rules null, transitions empty.
Use vehicle schema 2, workflow schema 1, adjustments schema 2; MonthlyPeriods null.

**P3 is a separately disclosed current-day authority seed exception**, required
by correction submission/review's `LoadPolicyAsync(Today(...))`. It is not a
past-only fixture or publication. If approval allows strictly past windows only,
omit P3 and leave historical correction/reconciliation/maintenance continuation
blocked. Do not fake the clock. Vehicle and allowance publication APIs require
future starts, so none can legitimately create these backdated policy versions.

| Additional fixture | Exact counts and values |
| --- | --- |
| Ordinary trip/fuel | 1 new Motorcycle `TEST-VO-CURRENT`, 1 custody for H issued 2026-10-19T03:00Z at 1000 and returned 2026-10-19T10:00Z at 1060; 1 commute row [2026-10-19, 2026-10-20), one-way 15/round-trip 30 km. One FieldVisit 2026-10-19T04:00Z-06:00Z, odometer 1000->1060, map 60, synthetic origin/destination/work/route/receipt labels. Creates 1 trip, 1 mileage, 1 fuel claim, 2 headers/decisions via real services: 30 eligible km, 1.50 litres, PKR 300.00. |
| Historical changed-amount source | 1 separate Motorcycle `TEST-VO-HISTORY`, 1 custody for H 2026-09-14T00:00Z at 1000 to 2026-09-21T18:00Z at 1200. Explicit testseed: 1 approved SiteToSite trip, 1 original mileage, 1 approved schema-1 fuel claim, 2 headers/decisions and matching original protected calculations. Trip 2026-09-15T18:00Z-2026-09-16T01:00Z, map/odometer 200 km (1000->1200), original excluded commute 0, 10 litres at P1 = PKR 1000.00. |
| Real correction/reconciliation | 1 correction proposal/decision and 1 fuel reconciliation proposal/decision. Split at 2026-09-15T19:00Z (Pakistan midnight): 100 km on each date, odometer 1000->1100->1200, map 100 each, CommuteDistanceId null. Corrected 5 litres x 100 + 5 x 200 = PKR 1500.00; difference +500.00; same 200 eligible km. No replacement mileage/fuel claim or financial settlement. |
| Equivalent maintenance chains | 8 API-created services: 4 Tuning and 4 EngineOverhaul. For each kind, approve two originals dated September 17/18 before correction, then two successors dated September 19/20 after correction. Odometer 1200, submitted outstation km 200, same workshop; consumption 50/100/150/200 km per kind. Actual creation/decision times are current and ordered; do not backdate audit times. No monthly adjustments or threshold transitions. |

Vehicle totals: **2 new vehicles, 2 custody rows, 1 commute, 2 trips, 2 mileage,
2 fuel claims, 8 services, 1 correction, 1 reconciliation, 14 approval headers
and 14 decisions**, plus the one definition/three policy versions.
Two of those headers/decisions belong to the historical testseed. Ordinary
vehicle writes use existing self/manage/custody/commute/trips/fuel/maintenance
permissions; incident and vehicle-policy grants are not needed to consume
explicitly seeded policies. Policy publication remains independently blocked.
Both closed custody intervals above are explicit testseed preconditions, not
claimed API custody acceptance. Issue creates an open-ended interval and checks
employee-wide overlap; the later retained V1 custody prevents assuming that
earlier closed intervals can be recreated through issue/return now. Do not edit
V1 to bypass that restriction. The two new assets can use CreateVehicleAsync.

Use H as beneficiary/original claimant, A as original trip/fuel/maintenance
reviewer and correction/reconciliation proposer, S as correction/reconciliation
reviewer. Thus **3 existing actors, 2 non-beneficiary review-capable actors**;
only one decision per proposal, and the original proposer cannot self-review.
Source/current configured attribution roles and current permission-owning role
must match. Preserve original rows, policy bytes, protected calculations and
decisions. Replay each new maintenance submission/approval while it is latest.
Changed historical kilometres, monthly composition and threshold transitions
are outside this bounded chain, even if an aggregate total appears sufficient.

## Seed provenance and approval boundary

Current ordinary approval emits mileage schema 3; ordinary fuel can still emit
schema 1. A current cross-day trip instead requires dated attribution; it cannot
produce the historical aggregate approval above. Schema-2 dated/schema-3
corrected fuel must not be relabelled as schema-1 history.

The historical corpus therefore needs a **separately reviewed explicit testseed**
manifest/generator; none exists as an executed artifact in this proposal.
No handcrafted protected approvals/receipts may be presented as real service
history. Seed-only calculations/decisions must be identified as synthetic
preconditions, retain correct identity/purpose/hash bindings through the real
private-store implementation, and have no invented live HTTP/audit provenance.
All new correction, reconciliation, maintenance and deployment evidence must
come from the existing services. Missing seed proof is a stop, not a placeholder.

Parent approval should cover the exact source seeds, custom-role provisioning,
deployment delegation repair and bounded outstation execution first; vehicle
continuation additionally needs explicit global-window/P3 and historical-corpus
approval. Combined source budget is 5 masters, 1 employment assignment,
2 policy definitions, 4 versions and 1 allowance assignment. V1 remains untouched.
Existing V1 host/runner only allows vehicle/custody; parent must prepare a new
bounded route/actor manifest and evidence reader before execution. No blanket HR
host/queue access is authorized by this document.

Source anchors: `Authentication/Access/AccessManagementService.cs`;
`Attendance/Common/AttendanceOperation.cs`; `HR/Allowances/DeploymentService.cs`,
`DeploymentClaims.cs`, `AllowancePolicyService.cs`; `HR/VehicleFuel/VehicleFuelService.cs`,
`VehiclePolicyPublication.cs`, `VehicleTripCorrections.cs`, `VehicleFuelReconciliation.cs`,
`VehicleFuelWorkflow.cs`, `VehicleConsumedMaintenance.cs` under
`src/Cosmix.Application`; `VehicleFuelRepository.cs`, `AllowanceRepository.cs`,
`DeploymentRepository.cs` under `src/Cosmix.Infrastructure/Repositories/HR`.
