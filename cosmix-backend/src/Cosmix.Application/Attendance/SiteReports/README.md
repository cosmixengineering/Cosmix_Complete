# SiteReports

Owner: Developer 2.

Source: SRS 7.1 and 11; MOB-009, MOB-013.

In-app arrival/departure, delay, access issue and incident reports with notes, location, evidence, sync status, idempotency and authorized review.

Status: immutable mobile report submission, self history/private evidence and scoped web reads are implemented.
Current device/employee identity, historical HR site assignment, protected audit/retry receipts,
scanned encrypted attachments and self-only notification-outbox staging are enforced.
Offline evidence remains explicitly unverified; reports do not create punches or calculate pay.

Review decisions remain unavailable pending confirmed authority. Genuine authenticated SQL,
scanner and notification-delivery acceptance remains separate. See
[contracts and verification](../../../../docs/attendance-completion/Site-Reports.md).
