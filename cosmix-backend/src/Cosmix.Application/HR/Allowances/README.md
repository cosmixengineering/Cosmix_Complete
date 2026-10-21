# Allowances

Owner: Developer 1.

Source: SRS 10.1; ALLOW-001 to ALLOW-004.

Meal eligibility and claims, continuous work-session cycles, evidence, decision and payroll status. Use attendance inputs from Developer 2. Prevent duplicate claims; stationed-site dinner amount remains unconfirmed.

Status: implemented for ALLOW-001..004. `AllowancePolicyRules`/codec own the published rules schema (stationed-site dinner amount stays nullable = MD-008 pending); `AllowancePolicyResolver`/`AllowancePolicyService` handle dated resolution and direct-HR publication/assignment; `AllowanceRules` evaluates continuous duty cycles, staff-category applicability and published amounts; `AllowanceService` runs sources/submit/history/detail/queue/cancel/decide against `IAllowanceRepository`; `AllowanceRegistration` wires the application services.

Out of scope: payroll settlement (`SettlementState` stays `NotScheduled`, `PayrollPeriodStart` stays null) and outstation/work-travel claims, which remain `deployment_workflow_unavailable` until the approved deployment workflow exists. EF Core and HTTP types stay out of this project.
