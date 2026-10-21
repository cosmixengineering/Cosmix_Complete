# Site Geofences implementation plan

Goal: deliver the user's Site Geofences prompt in the existing Desktop checkout, independently of the other developer's shifts/rosters work.

Design: extend the existing Site master with its existing append-only SiteGeofenceVersion model. Reuse fixed Admin/SuperAdmin plus hr.sites.manage for mutation, hr.sites.read for reference reads, and auth.self.read for the signed-in employee's own current site configuration. Recheck live sessions and assignments server-side. No role grants change.

New versions take effect strictly after the server's current business date and after the latest scheduled version. Use expected Site rowversion and expected latest boundary number for stale writes. Reject duplicate/backdated inserts and any intersecting locked future period. Dates are start-inclusive; the successor starts the next interval. Admin historical reads remain available after site deactivation.

The authorized administrator must supply explicit coordinates, radius, maximum GPS accuracy, a management-approval attestation/reference and reason. This captures external management approval; it does not invent an electronic approval chain. Reject excess stored decimal precision. No default attendance radius or accuracy is supplied.

The employee route accepts neither employee nor site ID nor historical date; it resolves today's single assignment. Return explicit not-assigned/not-configured/inactive states. A pure distance evaluator is preparatory geometry, not punch acceptance or attendance success.

Constraints: local files only; no Git/network operations, branches, worktrees, integration, commits or PRs. No database changes, migration rewriting, test projects, new packages, employee data or secrets. Keep other feature work untouched.

Execution and review:
- [x] Add bounded contracts and behavior checks without adding a test project.
- [x] Implement service, repository, controllers and minimal DI registration.
- [x] Add pure boundary evaluator with independent checks.
- [x] Verify dates, precision, permissions/self scope, stale writes, audit/retry behavior and locked periods using the service harness.
- [x] Run Release build, inspect EF model drift and exercise OpenAPI/unauthenticated routes locally.
- [x] Independently review implementation, fix findings, document executed checks and remaining operational acceptance.

Executed evidence and limits: ../Site-Geofences-Verification.md. Live SQL transaction/concurrency and authenticated HTTP acceptance remain separate from the completed offline/service/model checks.

Review focus: same-day historical rewriting, future-version conflicts, assignment end exclusivity/overlap, database decimal rounding, receipt replay after permission revocation.
