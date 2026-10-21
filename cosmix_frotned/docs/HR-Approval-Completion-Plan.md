# HR approval completion

Date: 8 October 2026.

The PM authorized extending the existing HR workspace with Admin approval UI and
remaining workflow acceptance. Work stays local in the existing frontend project.
Existing SMS testing mode remains in use. Backend contracts and protections remain
authoritative; no scanner bypass, fabricated evidence, migration or provider changes.

1. Add an authorized hiring submission queue and frozen submission review. Read
   financial/medical snapshots only when the reviewer opens permitted sections.
   Use the employee rowversion for decisions, not the submission revision number.
2. Add hiring Approve/Return/Reject with reasons, optional initial site for Site
   staff only, self-review denial, current-state checks and protected file review.
   Approval creates pending access; it does not silently activate credentials.
3. Add permitted Pending change/salary/status decisions to existing request details.
   Preserve HR cancellation/correction, stale-version checks and safe retry handling.
4. Verify actual Admin/HR navigation, denial, available decisions, historical and
   scheduled values, forms, responsive layouts and existing login. Use synthetic
   Cosmix_Testing records only. Synthetic account creation awaits the PM's specific
   exception to the earlier prohibition.
5. Run npm run verify, review the changes and report exact passing and blocked
   acceptance. Clean scan, complete hiring and assignment-backed transfer success
   require a real available scanner and appropriate approved test records.

Full access-role administration, credential provisioning and unrelated modules are
separate work. This increment displays the approval result and pending-access state.
