# Notifications

Owner: Developer 2.
Source: SRS 12; APR-005.

Recipient-scoped read/read-status, delivery state and approved safe payloads. No sensitive content on lock screens or email subjects.

Current-recipient in-app inbox and mark-read are implemented. Static safe templates and
transactional `INotificationOutbox` producers feed the durable worker; no external messaging.
See `docs/attendance-completion/notifications/README.md` for transaction contracts,
configuration and the offline-versus-SQL verification boundary.
