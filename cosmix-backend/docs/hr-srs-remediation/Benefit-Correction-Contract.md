# Consumed Benefit Correction

## Confirmed Boundary

On 2026-10-19 the owner approved removing unused invalid benefit credit while
retaining the consumed portion as an outstanding Admin-review record. This is
not permission to deduct salary, recover future leave, release payments, or
invent a recovery policy. The original award, leave use, encashment and their
policy evidence remain immutable.

Historical records do not contain a per-award spending allocation. The API
therefore does not assume "ordinary quota first", FIFO or another allocation
rule. An explicitly permitted Admin must attribute consumed minutes to exact
retained utilization/encashment ledger entries and record a reason. Review
evidence, not a guessed aggregate balance, is the authority for that allocation.

## API Sequence

1. Read the employee's awards using the existing Admin benefit history endpoint.
2. Read `GET /api/v1/leaves/admin/employees/{employeeId}/benefits/{id}/correction-impact`.
   It returns retained usage entries and an order-independent usage fingerprint.
3. Admin reviews the source records and submits
   `POST /api/v1/leaves/admin/employees/{employeeId}/benefits/{id}/correct`:

```json
{
  "expectedEvidenceFingerprint": "<exact award fingerprint>",
  "expectedUsageFingerprint": "<exact impact fingerprint>",
  "consumedUsage": [
    { "ledgerEntryId": "<verified usage entry ID>", "minutes": 120 }
  ],
  "reason": "Reviewed correction and source allocation"
}
```

The 120 minutes above are an example, not a default. `consumedUsage: []` means
the Admin explicitly reviewed the award as unconsumed; missing/null allocations
are rejected. Reuse the same idempotency key only for the identical request.
Use a new key after reviewing a changed fingerprint.

The existing sensitive `attendance.leaves.benefits.review` permission must have
explicit All scope. Under the approved 20 October delegation design, that
permission may come from a separately assigned custom role, but the same
account must independently hold a genuine, current fixed Admin assignment.
The service rechecks both facts; a similarly named custom role is not Admin
authority. Current web MFA, CSRF, session revalidation, no self review, audit,
receipts and recipient notification remain in force. No permission is
automatically granted.

## Retained Result

- One append-only adjustment withdraws the original award. The natural reversal
  key also prevents separate revocation and correction of the same award.
- `Correction.RemovedUnusedMinutes` is the award minus reviewed consumed minutes.
- `Correction.ConsumedMinutes` retains the original review obligation.
- `Correction.ProtectedConsumedMinutes` offsets only the exact still-consumed
  source usage. This protection is not cash eligible and grants no additional
  unused capacity.
- Status is `pending_admin_review` for a consumed portion, or `corrected_unused`
  when nothing was consumed. The correction itself is a completed HR recording
  workflow; recovery resolution still requires its own approved policy.
- If an exact source leave cancellation releases the consumption, the protected
  capacity disappears on subsequent reads. It cannot become a new spendable
  credit. The original review record is still retained.

The derived `ConsumptionOffset` marker distinguishes this protected debit offset
from spendable awards. Ordinary and cash balance calculations both retain the
offset, while new cash cannot consume it as an award. Dropping it from cash
valuation would incorrectly reduce the employee's untouched base quota.

Source minutes use each source policy's historical day duration. Conversion to
the award's retained duration must be exact; fractional-minute results are
rejected, not rounded. The original award's cash eligibility is required before
an encashment entry can be attributed to it. Allocation across correction
records cannot exceed the exact source consumption.

## Rejections

- Stale award/usage fingerprint; missing or altered retained evidence.
- Wrong employee, leave type, source before award, released source, invalid units,
  excessive allocation, duplicate allocation or repeated correction.
- Any outstanding encashment reservation in the year. Resolve it through the
  existing review workflow before correction; this API never cancels it silently.
- Locked source/intervening/current period. No reopening or finalized rewrite.
  Completed but unlocked years can be corrected because ordinary encashment is
  itself only available after the leave year ends. Simple unused revocation
  retains its existing current-year restriction.
- Storage/authority failure or usage changes during protected evidence IO.

Simple unused revocation also accepts exact fully released usage history now.
An unmatched release, duplicate release, wrong amount/type/policy/request or
net-zero aggregate without a valid source/release pair is not sufficient.

## Verification Boundary

See the dated continuation verification report for executed results. Standalone
service, evidence, quota and authorization tests are not authenticated SQL or
concurrent-HTTP acceptance. No migration is required for this correction slice:
it reuses the existing ledger, audit, private evidence, receipts and outbox.
No database migration or fixture write is authorized by this document.
