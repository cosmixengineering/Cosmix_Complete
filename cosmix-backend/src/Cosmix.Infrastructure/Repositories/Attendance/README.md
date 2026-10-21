# Attendance repositories

Implement Attendance feature repository interfaces from Application here. Add feature subfolders when a module grows. Use CosmixDbContext, async calls and cancellation tokens. Return materialized domain results, never IQueryable across the boundary. Call IUnitOfWork.SaveChangesAsync once at the application use-case boundary. EF SaveChanges supplies a transaction for one save; explicitly design transactions/outbox/idempotency for multi-step writes. Do not use a generic repository that permits modifying raw punches.
