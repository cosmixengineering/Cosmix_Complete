# Authentication persistence and providers

The implemented module uses the existing SQL Server context and serializable authentication lock for
shared mobile/web OTP, sessions, recovery and access management. Provider adapters remain configurable,
with no seeded password or success placeholder. See [Shared Authentication](../../../docs/Shared-Authentication.md).

AccessManagementRepository also loads/adds existing IdempotencyReceipt rows under the same transaction
as access mutations and audit. No new context, table or migration is needed. SQL stores only the request
digest and encrypted result reference; Application reauthorizes each receipt replay. Effective due
employment history denies access even if the lifecycle worker has not yet reconciled Employee.Status.
Actual SQL contention/rollback, approved live SMS, secure key/storage deployment and owner provisioning
are separate acceptance work; standalone in-memory checks are not proof of those operations.
