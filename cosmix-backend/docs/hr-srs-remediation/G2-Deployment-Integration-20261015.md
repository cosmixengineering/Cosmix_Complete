# G2 deployment integration contract

Updated 19 October 2026. Integrated source and standalone checks; no SQL, host,
provider or A02 packet execution. See `Continuation-20261019.md` for fresh results.

The user explicitly confirmed Admin approval of deployment and verified home-return
breaks. Daily rates, counting basis, partial days, remainders and rounding require
management-approved effective-dated configuration. A 40-hour trip has no inferred
payable-day count. Missing configuration is pending with null money, never zero.

## Parent-owned integration

Register these exact permission definitions in the shared catalog; grant nothing automatically:

| Code | Sensitive | Management | SelfService | Purpose |
| --- | --- | --- | --- | --- |
| attendance.deployments.read | true | true | false | Read scoped deployment, break and claim evidence |
| attendance.deployments.request | false | true | false | Request scoped deployments, verified home breaks and calculated claims |
| attendance.deployments.decide | true | true | false | Independently review deployment, home-break and claim requests as fixed Admin/SuperAdmin |

The service uses the existing AttendanceOperation transaction, web MFA/CSRF,
live grants, idempotency and fixed-system-role check. Ordinary HR permissions do
not confer review authority. Existing attendance.allowances.policy controls
Admin/SuperAdmin publication of the versioned configuration extension.

DI needed: AddScoped<DeploymentService>();
AddScoped<IDeploymentRepository, DeploymentRepository>();
Existing allowance resolver, source repository, summary reader, AttendanceOperation
and private file store are reused. No new worker or provider.

The model contains two new deployment types: OutstationDeployment and
DeploymentHomeBreakRequest. Claims reuse the existing AllowanceClaim and immutable
AllowanceClaimSubmission records; there is no duplicate OutstationDeploymentClaim
table. Registrations and permissions are integrated without automatic grants.
Parent-prepared migration 20261015144424_AddFinancialAdjustmentsAndOutstationDeployments
also contains the financial adjustment tables. It was not applied by this continuation.

Finance uses the exact independently approved allowance claim. A 19 October
regression exposed its generic lock rejecting the required protected deployment
decision. PayrollHandoffService now calls DeploymentService's retained-evidence
validator, requires the exact submission version and source identity, and only
then schedules the claim as InPayrollHandoff. Missing validator or damaged evidence
fails closed. It does not record payment. Exact retry schedules once. Generic meal
review cannot approve deployment claims; the separate Admin workflow is required.
WorkTravel and the legacy generic outstation submission shortcut remain unavailable;
the explicit deployment API is the implemented path. Actual HTTP/SQL and full
Preview -> Prepare -> Lock -> external settlement acceptance remain pending.

Source changes invalidate historical A02 source hashes. The historical packet is
not regenerated; the latest user instruction permits read-only A02 work only.
