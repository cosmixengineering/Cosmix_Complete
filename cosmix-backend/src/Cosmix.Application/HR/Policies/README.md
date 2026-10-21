# Policies

Owner: Developer 1; Developer 2 owns attendance calculation rules.

Source: SRS 8, 9, 10 and 19.

Configurable approved policy versions, scope, dates and activation. Office/site rules and amounts are not constants in clients. Unconfirmed formulas stay blocked; retain the version used for every calculation.

The user authorized local Attendance policy implementation on 12 October 2026. Typed explicit
settings validation is exposed through AttendancePolicySettingsService and the HR validation route.
It requires a separately assigned sensitive All-scope permission and current web MFA, audits access,
and returns canonical configuration identity without approval or persistence of a policy version.

The user then confirmed direct HR publication without a second reviewer. AttendancePolicyService
now creates definitions, previews/publishes complete version-3 rules and appends future assignments,
replacements and endings under the explicit publish permission. No salary or compensation is written.
Schema 3 adds explicitly reviewed ordinary-payroll-input settings and late-rule count/reset/salary-date
choices. Schemas 1 and 2 keep their exact canonical representation and historical readability.
The recommended financial-settings read endpoint provides form suggestions only: it never seeds,
publishes, assigns or substitutes values into incomplete configuration.
AttendancePolicyResolver resolves historical employee membership and fails closed on unsupported
authority, missing configuration or competing rules; it never invents population precedence.
See docs/attendance-completion/Direct-HR-Policies.md for the HTTP flow, compatibility and verification
boundaries. Standalone checks exercise synthetic service/authorization behavior and offline EF SQL
translation; SQL deployment and real employee/payroll acceptance remain separate.
