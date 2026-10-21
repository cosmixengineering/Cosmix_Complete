# BackgroundJobs

Owner: Developer 2.
Source: SRS 3, REP-001.

Durable notification/report/recalculation and cleanup jobs. Use persisted jobs/outbox and retries; no fire-and-forget Task.Run for required work.

The durable outbox processor is implemented with existing OutboxMessage lease/rowversion,
bounded retries and atomic in-database handler completion. The delivered handler creates
in-app notifications; report/recalculation/cleanup handlers require their own real workflows.
`AttendanceJobs.Enabled` defaults false. See the attendance-completion notification docs.
